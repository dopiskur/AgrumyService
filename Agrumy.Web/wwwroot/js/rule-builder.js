// Roadmap #396(4) - recursive rule-condition tree editor. Renders an interactive ConditionNode
// tree (comparison/interval/schedule/astronomical/ruleTriggered leaves, arbitrarily nested group
// nodes), and on form submit serializes the whole tree into one hidden RootConditionJson field -
// the server never sees individual form fields for this part, just the finished JSON tree, matching
// api.Models.ConditionNode's shape/casing (System.Text.Json Web defaults, i.e. camelCase).
(function () {
    // String keys are this file's own internal/UI vocabulary only - serializeNode() below maps every
    // one of them to the actual INT code api.Models' enums serialize as (System.Text.Json default,
    // no JsonStringEnumConverter on ConditionConfigJson.Options) before it ever reaches the hidden
    // field/server. Mirror api.Models.NodeType/ComparisonOperator/LogicalOperator/SensorMetric exactly.
    const NODE_TYPE_CODES = { comparison: 1, interval: 2, schedule: 3, group: 4, astronomical: 5, ruleTriggered: 6, rateOfChange: 7, difDisruption: 8 };
    const COMPARISON_OP_CODES = { greaterThan: 1, lessThan: 2, greaterThanOrEqual: 3, lessThanOrEqual: 4, equal: 5, between: 6 };
    const GROUP_OP_CODES = { and: 1, or: 2 };

    const NODE_TYPES = [
        ['comparison', 'Comparison'],
        ['interval', 'Interval'],
        ['schedule', 'Schedule'],
        ['astronomical', 'Astronomical (sunrise/sunset)'],
        ['ruleTriggered', 'Another rule fired'],
        ['rateOfChange', 'Rate of change (vs N hours ago)'],
        ['difDisruption', 'DIF disruption (night vs day temperature)'],
        ['group', 'Group (AND/OR of several conditions)'],
    ];

    class RuleTreeBuilder {
        constructor(root) {
            this.root = root;
            this.metrics = JSON.parse(root.dataset.metrics || '[]');
            this.referenceableRules = JSON.parse(root.dataset.referenceableRules || '[]');
            this.isNotification = root.dataset.notification === 'true';
            this.treeContainer = root.querySelector('.rule-tree-container');
            this.hiddenInput = root.querySelector('input[name="RootConditionJson"]');
            const addRow = document.createElement('div');
            addRow.innerHTML = '<button type="button" class="btn btn-sm btn-outline-secondary rt-add-root">+ Add first condition</button>';
            addRow.querySelector('.rt-add-root').addEventListener('click', () => this.setRoot(this.buildNodeEl('comparison')));
            this.treeContainer.appendChild(addRow);
            this.addRootRow = addRow;

            const form = root.closest('form');
            form.addEventListener('submit', (e) => {
                const tree = this.serialize();
                if (!tree) {
                    e.preventDefault();
                    alert('Add at least one condition first.');
                    return;
                }
                this.hiddenInput.value = JSON.stringify(tree);
            });
        }

        setRoot(nodeEl) {
            this.addRootRow.remove();
            this.treeContainer.appendChild(nodeEl);
        }

        allowedTypes() {
            return NODE_TYPES.filter(([value]) => {
                // Roadmap #398(2) - astronomical is now valid on both action types (AstronomicalRuleResolver runs on both paths), ruleTriggered/rateOfChange/difDisruption stay Notification-only.
                if (value === 'ruleTriggered' || value === 'rateOfChange' || value === 'difDisruption') return this.isNotification;
                return true;
            });
        }

        buildNodeEl(type) {
            const el = document.createElement('div');
            el.className = 'rt-node border rounded p-2 mb-2';
            el.dataset.type = type;

            const header = document.createElement('div');
            header.className = 'd-flex gap-2 align-items-center mb-2';
            const typeSelect = document.createElement('select');
            typeSelect.className = 'form-select form-select-sm rt-type';
            typeSelect.style.width = 'auto';
            this.allowedTypes().forEach(([value, label]) => {
                const opt = document.createElement('option');
                opt.value = value;
                opt.textContent = label;
                if (value === type) opt.selected = true;
                typeSelect.appendChild(opt);
            });
            typeSelect.addEventListener('change', () => {
                const replacement = this.buildNodeEl(typeSelect.value);
                el.replaceWith(replacement);
            });
            header.appendChild(typeSelect);

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'btn btn-sm btn-outline-danger ms-auto rt-remove';
            removeBtn.textContent = 'Remove';
            removeBtn.addEventListener('click', () => {
                if (el.parentElement === this.treeContainer) {
                    el.remove();
                    this.treeContainer.appendChild(this.addRootRow);
                } else {
                    el.remove();
                }
            });
            header.appendChild(removeBtn);
            el.appendChild(header);

            const body = document.createElement('div');
            body.className = 'rt-body';
            el.appendChild(body);
            this.renderBody(type, body, el);
            return el;
        }

        renderBody(type, body, el) {
            switch (type) {
                case 'comparison': {
                    // Option value IS the actual SensorMetric int code (as a string, per HTML) - serializeNode() just Number()s it back, no separate lookup table needed here.
                    const metricSelect = this.select('rt-metric', this.metrics.map(m => [String(m.value), m.label]));
                    const opSelect = this.select('rt-op', [
                        ['greaterThan', '>'], ['lessThan', '<'], ['greaterThanOrEqual', '>='],
                        ['lessThanOrEqual', '<='], ['equal', '='], ['between', 'between'],
                    ]);
                    const value1 = this.numberInput('rt-value1', 'Value');
                    const value2 = this.numberInput('rt-value2', 'Value 2 (between only)');
                    const hysteresis = this.numberInput('rt-hysteresis', 'Hysteresis (> / < only)');
                    body.append(this.row('Metric', metricSelect), this.row('Operator', opSelect), this.row('Value', value1), this.row('Value 2', value2), this.row('Hysteresis', hysteresis));
                    break;
                }
                case 'interval': {
                    const interval = this.numberInput('rt-interval', 'Interval (seconds)');
                    const intervalLength = this.numberInput('rt-intervalLength', 'On-duration (seconds)');
                    body.append(this.row('Interval (s)', interval), this.row('On for (s)', intervalLength));
                    break;
                }
                case 'schedule': {
                    body.append(this.daysOfWeekRow(), this.row('Start (sec since midnight)', this.numberInput('rt-start', 'Start')), this.row('Duration (sec)', this.numberInput('rt-duration', 'Duration')));
                    break;
                }
                case 'astronomical': {
                    body.append(this.daysOfWeekRow(), this.row('Sunrise offset (min)', this.numberInput('rt-sunriseOffset', 'Sunrise offset')), this.row('Sunset offset (min)', this.numberInput('rt-sunsetOffset', 'Sunset offset')));
                    break;
                }
                case 'ruleTriggered': {
                    const select = this.select('rt-referencedRuleId', this.referenceableRules.map(r => [String(r.id), r.name]));
                    body.append(this.row('Rule', select));
                    break;
                }
                case 'rateOfChange': {
                    const metricSelect = this.select('rt-metric', this.metrics.map(m => [String(m.value), m.label]));
                    body.append(
                        this.row('Metric', metricSelect),
                        this.row('Hours ago to compare against', this.numberInput('rt-windowHours', 'e.g. 3')),
                        this.row('Fires when the change reaches at least', this.numberInput('rt-changeThreshold', 'e.g. 5')));
                    break;
                }
                case 'difDisruption': {
                    body.append(
                        this.row('Night window (hours, ending now)', this.numberInput('rt-nightWindowHours', 'e.g. 4')),
                        this.row('Day window (hours, right before the night window)', this.numberInput('rt-dayWindowHours', 'e.g. 8')),
                        this.row('Fires when day-avg minus night-avg drops below', this.numberInput('rt-minDifDegrees', 'e.g. 5')));
                    break;
                }
                case 'group': {
                    const opSelect = this.select('rt-groupOperator', [['and', 'AND'], ['or', 'OR']]);
                    body.append(this.row('Operator', opSelect));
                    const children = document.createElement('div');
                    children.className = 'rt-children ms-4 mt-2';
                    body.appendChild(children);
                    const addRow = document.createElement('div');
                    addRow.innerHTML = '<button type="button" class="btn btn-sm btn-outline-secondary">+ Add child condition</button>';
                    addRow.querySelector('button').addEventListener('click', () => children.appendChild(this.buildNodeEl('comparison')));
                    body.appendChild(addRow);
                    break;
                }
            }
        }

        daysOfWeekRow() {
            const wrap = document.createElement('div');
            wrap.className = 'mb-2';
            const label = document.createElement('label');
            label.className = 'form-label small mb-0 d-block';
            label.textContent = 'Days of week';
            wrap.appendChild(label);
            ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].forEach((day, i) => {
                const id = 'rt-day-' + Math.random().toString(36).slice(2);
                const check = document.createElement('div');
                check.className = 'form-check form-check-inline';
                check.innerHTML = `<input class="form-check-input rt-dow" type="checkbox" value="${i}" id="${id}" checked><label class="form-check-label small" for="${id}">${day}</label>`;
                wrap.appendChild(check);
            });
            return wrap;
        }

        row(labelText, inputEl) {
            const wrap = document.createElement('div');
            wrap.className = 'mb-2';
            const label = document.createElement('label');
            label.className = 'form-label small mb-0 d-block';
            label.textContent = labelText;
            wrap.appendChild(label);
            wrap.appendChild(inputEl);
            return wrap;
        }

        select(cls, options) {
            const el = document.createElement('select');
            el.className = 'form-select form-select-sm ' + cls;
            options.forEach(([value, label]) => {
                const opt = document.createElement('option');
                opt.value = value;
                opt.textContent = label;
                el.appendChild(opt);
            });
            return el;
        }

        numberInput(cls, placeholder) {
            const el = document.createElement('input');
            el.type = 'number';
            el.step = 'any';
            el.className = 'form-control form-control-sm ' + cls;
            el.placeholder = placeholder;
            return el;
        }

        /// Walks the current DOM tree into a ConditionNode-shaped JS object (or null if no root yet).
        serialize() {
            const rootEl = this.treeContainer.querySelector(':scope > .rt-node');
            return rootEl ? this.serializeNode(rootEl) : null;
        }

        // Every field below is the actual wire-format INT code (or null) - NODE_TYPE_CODES/COMPARISON_OP_CODES/GROUP_OP_CODES map this file's own descriptive UI keys to api.Models' enum values; metric is already the int code as a string (see renderBody's comparison case), just Number()'d back.
        serializeNode(el) {
            const type = el.dataset.type;
            const body = el.querySelector(':scope > .rt-body');
            const num = (sel) => {
                const v = body.querySelector(sel)?.value;
                return v === undefined || v === '' ? null : Number(v);
            };
            switch (type) {
                case 'comparison': {
                    const opKey = body.querySelector('.rt-op')?.value;
                    return {
                        type: NODE_TYPE_CODES.comparison,
                        metric: num('.rt-metric'),
                        operator: opKey ? COMPARISON_OP_CODES[opKey] : null,
                        value1: num('.rt-value1'),
                        value2: num('.rt-value2'),
                        hysteresis: num('.rt-hysteresis'),
                    };
                }
                case 'interval':
                    return { type: NODE_TYPE_CODES.interval, interval: num('.rt-interval'), intervalLength: num('.rt-intervalLength') };
                case 'schedule':
                    return { type: NODE_TYPE_CODES.schedule, daysOfWeek: this.daysOfWeekMask(body), start: num('.rt-start'), duration: num('.rt-duration') };
                case 'astronomical':
                    return { type: NODE_TYPE_CODES.astronomical, daysOfWeek: this.daysOfWeekMask(body), sunriseOffsetMinutes: num('.rt-sunriseOffset'), sunsetOffsetMinutes: num('.rt-sunsetOffset') };
                case 'ruleTriggered': {
                    const v = body.querySelector('.rt-referencedRuleId')?.value;
                    return { type: NODE_TYPE_CODES.ruleTriggered, referencedRuleId: v ? Number(v) : null };
                }
                case 'rateOfChange':
                    return { type: NODE_TYPE_CODES.rateOfChange, metric: num('.rt-metric'), windowHours: num('.rt-windowHours'), changeThreshold: num('.rt-changeThreshold') };
                case 'difDisruption':
                    return { type: NODE_TYPE_CODES.difDisruption, nightWindowHours: num('.rt-nightWindowHours'), dayWindowHours: num('.rt-dayWindowHours'), minDifDegrees: num('.rt-minDifDegrees') };
                case 'group': {
                    const children = Array.from(body.querySelectorAll(':scope > .rt-children > .rt-node')).map(c => this.serializeNode(c));
                    const groupOpKey = body.querySelector('.rt-groupOperator')?.value;
                    return { type: NODE_TYPE_CODES.group, groupOperator: groupOpKey ? GROUP_OP_CODES[groupOpKey] : null, children };
                }
                default:
                    return null;
            }
        }

        daysOfWeekMask(body) {
            let mask = 0;
            body.querySelectorAll('.rt-dow:checked').forEach(cb => { mask |= (1 << Number(cb.value)); });
            return mask;
        }
    }

    document.querySelectorAll('.rule-tree-builder').forEach(el => new RuleTreeBuilder(el));
})();

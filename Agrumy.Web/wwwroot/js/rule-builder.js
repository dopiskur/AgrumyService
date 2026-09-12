// Recursive rule-condition tree editor. Renders an interactive ConditionNode
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
            this.simpleMode = root.dataset.simpleMode === 'true';
            const allMetrics = JSON.parse(root.dataset.metrics || '[]');
            // Simple mode hides derived metrics (VPD/DewPoint/DewPointSpread) - computed values, not something a beginner reads off a sensor.
            this.metrics = this.simpleMode ? allMetrics.filter(m => !m.derived) : allMetrics;
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
            // Simple mode - a rule is one flat "if metric compares to value" comparison, no grouping/schedule/astronomical/rate-of-change/other node types.
            if (this.simpleMode) {
                return NODE_TYPES.filter(([value]) => value === 'comparison');
            }
            return NODE_TYPES.filter(([value]) => {
                // Astronomical is now valid on both action types (AstronomicalRuleResolver runs on both paths), ruleTriggered/rateOfChange/difDisruption stay Notification-only.
                if (value === 'ruleTriggered' || value === 'rateOfChange' || value === 'difDisruption') return this.isNotification;
                return true;
            });
        }

        // isGroupChild: true for a node living inside a group's .rt-children - only those get a drag handle
        // and swap-with-sibling arrows, since a lone top-level node (Simple mode's single condition, or a
        // fresh root before it's wrapped in a group) has no siblings to rearrange against.
        buildNodeEl(type, isGroupChild = false) {
            const el = document.createElement('div');
            el.className = 'rt-node border rounded p-2 mb-2';
            el.dataset.type = type;

            const header = document.createElement('div');
            header.className = 'd-flex gap-2 align-items-center mb-2';

            if (isGroupChild) {
                el.draggable = true;
                const handle = document.createElement('span');
                handle.className = 'rt-drag-handle bi bi-grip-vertical';
                handle.title = 'Drag to reposition';
                header.appendChild(handle);

                const swapPrev = document.createElement('button');
                swapPrev.type = 'button';
                swapPrev.className = 'btn btn-sm btn-outline-secondary rt-swap-prev';
                swapPrev.title = 'Swap with previous sibling';
                swapPrev.textContent = '⬅'; // left arrow - direction is illustrative only, works the same in an AND (vertical) group as "swap with the one above"
                swapPrev.addEventListener('click', () => this.swapWithSibling(el, -1));
                header.appendChild(swapPrev);

                const swapNext = document.createElement('button');
                swapNext.type = 'button';
                swapNext.className = 'btn btn-sm btn-outline-secondary rt-swap-next';
                swapNext.title = 'Swap with next sibling';
                swapNext.textContent = '➡'; // right arrow - "swap with the one below" in an AND group
                swapNext.addEventListener('click', () => this.swapWithSibling(el, 1));
                header.appendChild(swapNext);
            }

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
                const replacement = this.buildNodeEl(typeSelect.value, isGroupChild);
                el.replaceWith(replacement);
            });
            // Simple mode only ever allows one type - a single-option dropdown is just clutter, not a real choice.
            if (!this.simpleMode) {
                header.appendChild(typeSelect);
            }

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

        // Adjacent-position swap - direction -1/+1 within the node's own parent container, independent of
        // whether that container is currently laid out as a row (OR) or column (AND). A no-op past either end.
        swapWithSibling(nodeEl, direction) {
            const parent = nodeEl.parentElement;
            const siblings = Array.from(parent.querySelectorAll(':scope > .rt-node'));
            const targetIndex = siblings.indexOf(nodeEl) + direction;
            if (targetIndex < 0 || targetIndex >= siblings.length) {
                return;
            }
            const target = siblings[targetIndex];
            if (direction < 0) {
                parent.insertBefore(nodeEl, target);
            } else {
                parent.insertBefore(target, nodeEl);
            }
        }

        // Free-form drag-and-drop reordering within a group's children container, on top of the swap arrows
        // above - classic "insert relative to nearest sibling" native HTML5 DnD, no library.
        wireReorderable(container) {
            let draggedEl = null;
            container.addEventListener('dragstart', (e) => {
                const node = e.target.closest('.rt-node');
                if (!node || node.parentElement !== container) {
                    return;
                }
                draggedEl = node;
                e.dataTransfer.effectAllowed = 'move';
                e.dataTransfer.setData('text/plain', ''); // Firefox requires setData in dragstart or the drag never starts
                node.classList.add('rt-dragging');
            });
            container.addEventListener('dragend', () => {
                if (draggedEl) {
                    draggedEl.classList.remove('rt-dragging');
                }
                draggedEl = null;
            });
            container.addEventListener('dragover', (e) => {
                if (!draggedEl) {
                    return;
                }
                e.preventDefault();
                const isRow = container.classList.contains('rt-children-or');
                const coord = isRow ? e.clientX : e.clientY;
                const afterEl = this.dragAfterElement(container, isRow, coord);
                if (afterEl == null) {
                    container.appendChild(draggedEl);
                } else {
                    container.insertBefore(draggedEl, afterEl);
                }
            });
            // The actual move already happened live during dragover - this only exists to stop the browser's
            // default drop behavior (e.g. treating the dataTransfer text as a navigation).
            container.addEventListener('drop', (e) => e.preventDefault());
        }

        // Which existing child the dragged node should land BEFORE, given the pointer's current position along
        // the container's layout axis (X for a row/OR group, Y for a column/AND group) - null means "at the end".
        dragAfterElement(container, isRow, coord) {
            const candidates = Array.from(container.querySelectorAll(':scope > .rt-node:not(.rt-dragging)'));
            return candidates.reduce((closest, candidate) => {
                const box = candidate.getBoundingClientRect();
                const center = isRow ? box.left + box.width / 2 : box.top + box.height / 2;
                const offset = coord - center;
                return (offset < 0 && offset > closest.offset) ? { offset, element: candidate } : closest;
            }, { offset: -Infinity, element: null }).element;
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
                    // Radios instead of a dropdown so the operator choice IS the spatial layout toggle below,
                    // not a separate control describing it. Unique name per group node instance so several
                    // group nodes on the same page don't share a radio group.
                    const radioName = 'rt-groupOperator-' + Math.random().toString(36).slice(2);
                    const opWrap = document.createElement('div');
                    [['and', 'AND'], ['or', 'OR']].forEach(([value, label]) => {
                        const id = radioName + '-' + value;
                        const wrap = document.createElement('div');
                        wrap.className = 'form-check form-check-inline';
                        wrap.innerHTML = `<input class="form-check-input rt-groupOperator" type="radio" name="${radioName}" value="${value}" id="${id}"${value === 'and' ? ' checked' : ''}>` +
                            `<label class="form-check-label" for="${id}">${label}</label>`;
                        opWrap.appendChild(wrap);
                    });
                    body.append(this.row('Operator', opWrap));

                    // AND stacks children vertically (below), OR lays them out side by side (right) - see the
                    // .rt-children CSS in _RuleEditor.cshtml. Switching the radio re-flows every existing child
                    // into the new layout immediately, which IS "moving the block" per the roadmap wording -
                    // no separate animation/reposition step needed, CSS flex-direction does it.
                    const children = document.createElement('div');
                    children.className = 'rt-children rt-children-and ms-4 mt-2';
                    body.appendChild(children);
                    this.wireReorderable(children);
                    opWrap.querySelectorAll('.rt-groupOperator').forEach((radio) => radio.addEventListener('change', () => {
                        children.className = 'rt-children ms-4 mt-2 ' + (radio.value === 'or' ? 'rt-children-or' : 'rt-children-and');
                    }));

                    const addRow = document.createElement('div');
                    addRow.innerHTML = '<button type="button" class="btn btn-sm btn-outline-secondary">+ Add child condition</button>';
                    addRow.querySelector('button').addEventListener('click', () => children.appendChild(this.buildNodeEl('comparison', true)));
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
                    // DOM order already reflects whatever dragging/swapping rearranged it to.
                    const children = Array.from(body.querySelectorAll(':scope > .rt-children > .rt-node')).map(c => this.serializeNode(c));
                    const groupOpKey = body.querySelector('.rt-groupOperator:checked')?.value;
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

    // "+ Add rule" wizard: step 1 is Name/Description/Safety only, step 2 (Target%/Subject+Body, and the
    // condition tree) only appears after Next - the tree builder above never needed changing for this, it's the
    // same form either way, just progressively revealed instead of all shown behind one <details> disclosure.
    document.querySelectorAll('.rule-add-wizard').forEach(wizard => {
        const toggle = wizard.querySelector('.rule-add-toggle');
        const form = wizard.querySelector('.rule-add-form');
        const steps = Array.from(form.querySelectorAll(':scope > .rule-wizard-step'));

        function showStep(index) {
            steps.forEach((step, i) => { step.hidden = i !== index; });
        }

        toggle.addEventListener('click', () => {
            const opening = form.hidden;
            form.hidden = !form.hidden;
            if (opening) {
                showStep(0);
            }
        });

        form.querySelectorAll('.rule-wizard-next').forEach(btn => btn.addEventListener('click', () => {
            const step = btn.closest('.rule-wizard-step');
            // Only validate what's actually visible on this step - required fields on a still-hidden later step must not block Next.
            const invalid = Array.from(step.querySelectorAll('input[required]')).find(input => !input.checkValidity());
            if (invalid) {
                invalid.reportValidity();
                return;
            }
            showStep(steps.indexOf(step) + 1);
        }));
        form.querySelectorAll('.rule-wizard-back').forEach(btn => btn.addEventListener('click', () => {
            showStep(steps.indexOf(btn.closest('.rule-wizard-step')) - 1);
        }));
    });
})();

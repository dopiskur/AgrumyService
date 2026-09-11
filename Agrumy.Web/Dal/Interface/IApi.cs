using Agrumy.Shared.Models;
using Refit;

namespace Agrumy.Web.Dal.Interface
{
    /// Declarative Refit client for Agrumy.Api (AddRefitClient in Program.cs) - BearerTokenHandler attaches the JWT so no method takes a token param, and non-success responses raise ApiException (RefitConfig) carrying the response body.
    public interface IApi
    {
        // ---- User ----------------------------------------------------------

        [Post("/api/User/Login")]
        Task<UserLoginResult?> UserLogin([Body] UserLogin userLogin);

        /// Tenant-import counterpart to Login - proves identity with the old (imported) password since MustChangePassword blocks Login itself (428, Agrumy.Shared.Models.User.MustChangePassword).
        [Post("/api/User/ForceChangePassword")]
        Task<UserLoginResult?> UserForceChangePassword([Body] UserForceChangePassword value);

        /// Whether the bootstrap Global Admin still has no password - LoginController checks this on every anonymous page load to pick the login form or the first-run "set password" screen.
        [Get("/api/User/BootstrapPending")]
        Task<bool> BootstrapPending();

        /// The one-shot call that gives the bootstrap Global Admin a real password - see Agrumy.Shared.Models.BootstrapAdminSetPassword for why it takes no login/email.
        [Post("/api/User/BootstrapSetPassword")]
        Task BootstrapSetPassword([Body] BootstrapAdminSetPassword value);

        // Task (not Task<User>) on purpose: no caller reads the created record, same convention as the other write endpoints.
        [Post("/api/User/Register")]
        Task UserRegister([Body] UserRegistration registration);

        [Post("/api/User")]
        Task UserAdd([Body] UserAdd user);

        [Put("/api/User")]
        Task UserUpdate([Body] UserUpdate userUpdate);

        [Delete("/api/User")]
        Task UserDelete(int? idUser);

        [Get("/api/User")]
        Task<User> UserGet(int? idUser);

        [Get("/api/User/All")]
        Task<IEnumerable<User>> UsersGet();

        /// The caller's own record (self-scoped by the JWT server-side) - profile page prefill and the display time zone for UTC-to-local conversion.
        [Get("/api/User/Self")]
        Task<User> UserGetSelf();

        /// Self-service profile write - FirstName/LastName/TimeZone only, identity from the attached JWT (see UserApiController.UserProfileSet).
        [Put("/api/User/Profile")]
        Task UserProfileSet([Body] UserProfileUpdate value);

        /// Full (EventType x Channel) matrix for the caller, defaults resolved server-side - see UserApiController.NotificationPreferencesGet.
        [Get("/api/User/NotificationPreferences")]
        Task<IList<UserNotificationPreference>> NotificationPreferencesGet();

        /// One toggle at a time.
        [Put("/api/User/NotificationPreferences")]
        Task NotificationPreferenceSet([Body] UserNotificationPreference value);

        /// Password-change flow - proves the caller still knows the old password, identity otherwise comes from the attached JWT.
        [Post("/api/User/ChangePassword")]
        Task ChangePassword([Body] UserSetPassword value);

        /// Rotates the caller's device-registration PIN (24h validity, multi-use - not consumed by the first successful registration).
        [Post("/api/User/DevicePin")]
        Task<DevicePinResult> DevicePinGenerate();

        /// The given user's composable role set (a user can hold several).
        [Get("/api/User/UserRoles")]
        Task<List<string>> UserRolesGet(int idUser);

        [Put("/api/User/UserRoles")]
        Task UserRolesSet([Body] UserRolesUpdate value);

        // ---- Device --------------------------------------------------------

        [Get("/api/Device/All")]
        Task<IEnumerable<DeviceDto>> DevicesGet();

        [Get("/api/Device")]
        Task<DeviceDto> DeviceGet(int? idDevice);

        [Put("/api/Device")]
        Task DeviceUpdate([Body] DeviceDto? device);

        [Delete("/api/Device")]
        Task DeviceDelete(int? idDevice);

        [Get("/api/Device/Sensor")]
        Task<DeviceConfigSensor> DeviceConfigSensorGet(int? deviceConfigSensorID);

        [Get("/api/Device/SensorDetection")]
        Task<DeviceSensorDetectionResult?> DeviceSensorDetectionResultGet(int idDevice);

        [Get("/api/Device/Controller")]
        Task<DeviceConfigController> DeviceConfigControllerGet(int? deviceConfigControllerID);

        [Put("/api/Device/Sensor")]
        Task DeviceConfigSensorUpdate([Body] DeviceUpdate deviceUpdate);

        [Put("/api/Device/Controller")]
        Task DeviceConfigControllerUpdate([Body] DeviceUpdate deviceUpdate);

        [Get("/api/Device/Role")]
        Task<IEnumerable<DeviceRole>> DeviceRoleGet();

        [Get("/api/Device/Type")]
        Task<IEnumerable<DeviceType>> DeviceTypeGet();

        [Post("/api/Simulation/Device")]
        Task<DeviceDto> SimulationDeviceCreate();

        [Get("/api/Simulation/Device")]
        Task<IList<int>> SimulationDeviceList();

        [Delete("/api/Simulation/Device/{idDevice}")]
        Task SimulationDeviceDelete(int idDevice);

        [Get("/api/Device/Simulation/{idDevice}")]
        Task<DeviceSimulation> DeviceSimulationGet(int idDevice);

        [Put("/api/Device/Simulation/{idDevice}")]
        Task DeviceSimulationSet(int idDevice, [Body] DeviceSimulation value);

        // ---- Simulation sessions (roadmap #403) ----------------------------

        [Post("/api/Simulation/Session")]
        Task<SimulationSession> SimulationSessionCreate([Body] SimulationSessionCreateRequest request);

        [Get("/api/Simulation/Session")]
        Task<IList<SimulationSession>> SimulationSessionList();

        [Get("/api/Simulation/Session/{idSimulationSession}")]
        Task<SimulationSession> SimulationSessionGet(int idSimulationSession);

        [Post("/api/Simulation/Session/{idSimulationSession}/Start")]
        Task SimulationSessionStart(int idSimulationSession, [Body] SimulationSessionStartRequest request);

        [Post("/api/Simulation/Session/{idSimulationSession}/Stop")]
        Task SimulationSessionStop(int idSimulationSession);

        [Delete("/api/Simulation/Session/{idSimulationSession}")]
        Task SimulationSessionDelete(int idSimulationSession);

        [Post("/api/Simulation/Session/{idSimulationSession}/Device/{idDevice}")]
        Task SimulationSessionDeviceAdd(int idSimulationSession, int idDevice);

        [Delete("/api/Simulation/Session/{idSimulationSession}/Device/{idDevice}")]
        Task SimulationSessionDeviceRemove(int idSimulationSession, int idDevice);

        [Get("/api/Simulation/Session/{idSimulationSession}/Group")]
        Task<IList<SimulationGroup>> SessionGroupsGet(int idSimulationSession);

        [Post("/api/Simulation/Session/{idSimulationSession}/Group")]
        Task<SimulationGroup> SessionGroupAdd(int idSimulationSession, [Body] SimulationGroup group);

        [Put("/api/Simulation/Session/{idSimulationSession}/Group/{idGroup}")]
        Task SessionGroupUpdate(int idSimulationSession, int idGroup, [Body] SimulationGroup group);

        [Delete("/api/Simulation/Session/{idSimulationSession}/Group/{idGroup}")]
        Task SessionGroupDelete(int idSimulationSession, int idGroup);

        [Get("/api/Device/TypeService")]
        Task<IEnumerable<DeviceTypeService>> DeviceTypeServiceGet();

        [Get("/api/Device/TypeRelay")]
        Task<IEnumerable<DeviceTypeRelay>> DeviceTypeRelayGet();

        [Get("/api/Device/TypeSensor")]
        Task<IEnumerable<DeviceTypeSensor>> DeviceTypeSensorGet();

        // ---- Device events -----------------------------------

        [Get("/api/Device/Events")]
        Task<IList<DeviceEvent>> DeviceEventsGet(int? idDevice);

        [Put("/api/Device/Event/{idEventDevice}/Acknowledge")]
        Task DeviceEventAcknowledge(int idEventDevice);

        // ---- Fleet dashboard ----------------------------------

        [Get("/api/Device/Fleet")]
        Task<IList<DeviceFleetStatus>> DeviceFleetGet();

        [Get("/api/Device/FleetStatus")]
        Task<DeviceFleetStatus> DeviceFleetStatusGet(int idDevice);

        // ---- Gateway ------------------------------------------

        [Get("/api/Gateway/All")]
        Task<IList<DeviceDto>> GatewaysGetAll();

        [Get("/api/Gateway/DeviceMapping/All")]
        Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingGetAll(int idGatewayDevice);

        [Post("/api/Gateway/DeviceMapping")]
        Task GatewayDeviceMappingAdd([Body] GatewayDeviceMapping value);

        [Delete("/api/Gateway/DeviceMapping")]
        Task GatewayDeviceMappingDelete(int idGatewayDeviceMapping, int idGatewayDevice);

        // ---- Farm (roadmap #384) --------------------------

        [Get("/api/DeviceFarmUnit/Farm/All")]
        Task<IList<DeviceFarm>> DeviceFarmsGet();

        [Get("/api/DeviceFarmUnit/Farm")]
        Task<DeviceFarm> DeviceFarmGet(int? idDeviceFarm);

        [Post("/api/DeviceFarmUnit/Farm")]
        Task<DeviceFarm> DeviceFarmAdd([Body] DeviceFarm farm);

        [Put("/api/DeviceFarmUnit/Farm")]
        Task DeviceFarmUpdate([Body] DeviceFarm farm);

        /// Persists the Farms page's drag-and-drop card order - full replacement of every listed farm's DisplayOrder by index, not a partial patch.
        [Post("/api/DeviceFarmUnit/Farm/Reorder")]
        Task DeviceFarmsReorder([Body] List<int> orderedFarmIds);

        [Delete("/api/DeviceFarmUnit/Farm")]
        Task DeviceFarmDelete(int? idDeviceFarm);

        // ---- Recycle Bin (roadmap #409) --------------------------

        [Get("/api/RecycleBin/Device")]
        Task<IList<DeviceDto>> RecycleBinDevicesGet();

        [Get("/api/RecycleBin/Farm")]
        Task<IList<DeviceFarm>> RecycleBinFarmsGet();

        [Post("/api/RecycleBin/Device/{idDevice}/Restore")]
        Task RecycleBinDeviceRestore(int idDevice);

        [Post("/api/RecycleBin/Farm/{idDeviceFarm}/Restore")]
        Task RecycleBinFarmRestore(int idDeviceFarm);

        [Get("/api/RecycleBin/Device/PendingPurge")]
        Task<IList<DeviceDto>> RecycleBinDevicesPendingPurgeGet();

        [Get("/api/RecycleBin/Farm/PendingPurge")]
        Task<IList<DeviceFarm>> RecycleBinFarmsPendingPurgeGet();

        // ---- Recycle Bin permanent delete (roadmap #427) ----------

        [Post("/api/RecycleBin/Device/{idDevice}/PurgeNow")]
        Task RecycleBinDevicePurgeNow(int idDevice, [Body] RecycleBinPurgeRequest request);

        [Post("/api/RecycleBin/Farm/{idDeviceFarm}/PurgeNow")]
        Task RecycleBinFarmPurgeNow(int idDeviceFarm, [Body] RecycleBinPurgeRequest request);

        [Post("/api/RecycleBin/Empty")]
        Task<RecycleBinEmptyResult> RecycleBinEmpty([Body] RecycleBinPurgeRequest request);

        // ---- Unit/Zone -----------------------------------

        [Get("/api/DeviceFarmUnit/All")]
        Task<IList<DeviceFarmUnit>> DeviceFarmUnitsGet();

        [Get("/api/DeviceFarmUnit")]
        Task<DeviceFarmUnit> DeviceFarmUnitGet(int? idDeviceFarmUnit);

        [Post("/api/DeviceFarmUnit")]
        Task<DeviceFarmUnit> DeviceFarmUnitAdd([Body] DeviceFarmUnit unit);

        [Put("/api/DeviceFarmUnit")]
        Task DeviceFarmUnitUpdate([Body] DeviceFarmUnit unit);

        [Delete("/api/DeviceFarmUnit")]
        Task DeviceFarmUnitDelete(int? idDeviceFarmUnit);

        /// Persists the Farms page's drag-and-drop unit cube order - full replacement of every listed unit's DisplayOrder by index, not a partial patch.
        [Post("/api/DeviceFarmUnit/Reorder")]
        Task DeviceFarmUnitsReorder([Body] List<int> orderedUnitIds);

        [Get("/api/DeviceFarmUnit/Zone")]
        Task<IList<DeviceFarmUnitZone>> DeviceFarmUnitZonesGet(int? idDeviceFarmUnit);

        [Post("/api/DeviceFarmUnit/Zone")]
        Task<DeviceFarmUnitZone> DeviceFarmUnitZoneAdd([Body] DeviceFarmUnitZone zone);

        [Put("/api/DeviceFarmUnit/Zone")]
        Task DeviceFarmUnitZoneUpdate([Body] DeviceFarmUnitZone zone);

        [Delete("/api/DeviceFarmUnit/Zone")]
        Task DeviceFarmUnitZoneDelete(int? idDeviceFarmUnitZone);

        [Get("/api/DeviceFarmUnit/ZoneById")]
        Task<DeviceFarmUnitZone> DeviceFarmUnitZoneGetById(int? idDeviceFarmUnitZone);

        [Put("/api/DeviceFarmUnit/Zone/{idDeviceFarmUnitZone}/Migrate")]
        Task DeviceFarmUnitZoneMigrate(int idDeviceFarmUnitZone, int idTargetDeviceFarmUnit);

        // Roadmap #238 - saves independently of DeviceFarmUnitZoneUpdate above.
        [Put("/api/DeviceFarmUnit/Zone/{idDeviceFarmUnitZone}/Widgets")]
        Task DeviceFarmUnitZoneWidgetsSet(int idDeviceFarmUnitZone, [Body] List<DashboardWidget> widgets);

        // ---- Rules (Zone/Unit/Global scope, roadmap #212) ------

        [Get("/api/DeviceFarmUnit/Zone/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> DeviceFarmUnitZoneRulesGet(int? idDeviceFarmUnitZone);

        [Post("/api/DeviceFarmUnit/Zone/Rule")]
        Task<RuleAddResult> DeviceFarmUnitZoneRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Zone/Rule")]
        Task DeviceFarmUnitZoneRuleDelete(int? idDeviceFarmUnitZoneRule);

        // ---- Horticulture Catalog -------------------------------

        [Get("/api/HorticultureCatalog")]
        Task<IList<HorticultureCatalogEntry>> HorticultureCatalogGet(HorticultureCatalogType type);

        [Get("/api/HorticultureCatalog/ById")]
        Task<HorticultureCatalogEntry> HorticultureCatalogGetById(HorticultureCatalogType type, int id);

        [Post("/api/HorticultureCatalog")]
        Task<HorticultureCatalogEntry> HorticultureCatalogAdd(HorticultureCatalogType type, [Body] HorticultureCatalogEntry entry);

        [Put("/api/HorticultureCatalog")]
        Task HorticultureCatalogUpdate(HorticultureCatalogType type, [Body] HorticultureCatalogEntry entry);

        [Delete("/api/HorticultureCatalog")]
        Task HorticultureCatalogDelete(HorticultureCatalogType type, int id);

        [Post("/api/DeviceFarmUnit/Zone/ApplyHorticultureCatalog")]
        Task<HorticultureCatalogApplyResult> HorticultureCatalogApplyToZone(int idDeviceFarmUnitZone, HorticultureCatalogType catalogType, int catalogId);

        [Post("/api/DeviceFarmUnit/Zone/ApplyDayNightPreset")]
        Task<DayNightPresetApplyResult> DayNightPresetApplyToZone(int idDeviceFarmUnitZone, [Body] DayNightTargetPresetRequest request);

        [Get("/api/DeviceFarmUnit/Unit/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> DeviceFarmUnitRulesGet(int? idDeviceFarmUnit);

        [Post("/api/DeviceFarmUnit/Unit/Rule")]
        Task<RuleAddResult> DeviceFarmUnitRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Unit/Rule")]
        Task DeviceFarmUnitRuleDelete(int? idDeviceFarmUnitZoneRule);

        [Get("/api/DeviceFarmUnit/Farm/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> DeviceFarmRulesGet(int? idDeviceFarm);

        [Post("/api/DeviceFarmUnit/Farm/Rule")]
        Task<RuleAddResult> DeviceFarmRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Farm/Rule")]
        Task DeviceFarmRuleDelete(int? idDeviceFarmUnitZoneRule);

        [Get("/api/DeviceFarmUnit/Global/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> GlobalRulesGet();

        [Post("/api/DeviceFarmUnit/Global/Rule")]
        Task<RuleAddResult> GlobalRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Global/Rule")]
        Task GlobalRuleDelete(int? idDeviceFarmUnitZoneRule);

        // ---- Open-Field: Crop/Parcel --------------------------

        [Post("/api/FarmOpenfield")]
        Task<DeviceFarm> FarmOpenfieldCreate([Body] string? farmName);

        [Get("/api/FarmOpenfield/All")]
        Task<IList<FarmOpenfield>> FarmOpenfieldsGet();

        [Get("/api/FarmOpenfield/FarmParcel/All")]
        Task<IList<FarmParcel>> FarmParcelsGet(int idFarmOpenfield);

        [Get("/api/FarmOpenfield/FarmParcel/{idFarmParcel}/Zones")]
        Task<IList<FarmParcelZone>> FarmParcelZonesGet(int idFarmParcel);

        [Get("/api/FarmOpenfield/Crop/All")]
        Task<IList<Sowing>> CropsGet();

        [Get("/api/FarmOpenfield/Crop/Dashboard")]
        Task<IList<SowingDashboard>> CropDashboardGet();

        [Get("/api/FarmOpenfield/Crop")]
        Task<Sowing> CropGet(int? idSowing);

        [Post("/api/FarmOpenfield/Crop")]
        Task<Sowing> CropAdd([Body] Sowing crop);

        [Put("/api/FarmOpenfield/Crop")]
        Task CropUpdate([Body] Sowing crop);

        [Delete("/api/FarmOpenfield/Crop")]
        Task CropDelete(int? idSowing);

        [Post("/api/FarmOpenfield/Sowing/Start")]
        Task SowingStart([Body] SowingStartRequest request);

        [Post("/api/FarmOpenfield/Sowing/Close")]
        Task SowingClose([Body] SowingCloseRequest request);

        [Get("/api/FarmOpenfield/Parcel")]
        Task<IList<FarmParcelZone>> ParcelsGet(int? idSowing);

        [Get("/api/FarmOpenfield/Crop/Parcel/Dashboard")]
        Task<IList<FarmParcelZoneDashboard>> ParcelDashboardListGet(int? idSowing);

        [Get("/api/FarmOpenfield/ParcelById")]
        Task<FarmParcelZone> ParcelGetById(int? idFarmParcelZone);

        /// Creates a FarmParcel and its first zone in one call (D3) - replaces the pre-restructure "add a parcel under a crop" endpoint.
        [Post("/api/FarmOpenfield/FarmParcel")]
        Task<FarmParcelZone> FarmParcelAdd(int idFarmOpenfield, string farmParcelName);

        [Put("/api/FarmOpenfield/Parcel")]
        Task ParcelUpdate([Body] FarmParcelZone parcel);

        [Delete("/api/FarmOpenfield/Parcel")]
        Task ParcelDelete(int? idFarmParcelZone);

        [Post("/api/FarmOpenfield/Parcel/{idFarmParcelZone}/Split")]
        Task<IList<FarmParcelZone>> ParcelSplit(int idFarmParcelZone, [Body] List<string> newZoneNames);

        [Post("/api/FarmOpenfield/Parcel/Merge")]
        Task<FarmParcelZone> ParcelMerge([Body] ParcelMergeRequest request);

        // Saves independently of ParcelUpdate above, same reasoning as DeviceFarmUnitZoneWidgetsSet.
        [Put("/api/FarmOpenfield/Parcel/{idFarmParcelZone}/Widgets")]
        Task ParcelWidgetsSet(int idFarmParcelZone, [Body] List<DashboardWidget> widgets);

        [Post("/api/FarmOpenfield/Assign")]
        Task ParcelDeviceAssign([Body] DeviceParcelAssignment body);

        [Post("/api/FarmOpenfield/Unassign")]
        Task ParcelDeviceUnassign(int? idDevice);

        [Get("/api/DeviceFarmUnit/Crop/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> SowingRulesGet(int? idSowing);

        [Post("/api/DeviceFarmUnit/Crop/Rule")]
        Task<RuleAddResult> SowingRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Crop/Rule")]
        Task SowingRuleDelete(int? idDeviceFarmUnitZoneRule);

        [Get("/api/DeviceFarmUnit/Parcel/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> FarmParcelZoneRulesGet(int? idFarmParcelZone);

        [Post("/api/DeviceFarmUnit/Parcel/Rule")]
        Task<RuleAddResult> FarmParcelZoneRuleAdd([Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/DeviceFarmUnit/Parcel/Rule")]
        Task FarmParcelZoneRuleDelete(int? idDeviceFarmUnitZoneRule);

        [Get("/api/Simulation/Session/{idSimulationSession}/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> SessionRulesGet(int idSimulationSession);

        [Post("/api/Simulation/Session/{idSimulationSession}/Rule")]
        Task<int> SessionRuleAdd(int idSimulationSession, [Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/Simulation/Session/{idSimulationSession}/Rule/{idRule}")]
        Task SessionRuleDelete(int idSimulationSession, int idRule);

        // ---- Experiments ----------------------------

        [Post("/api/Experiment")]
        Task<Experiment> ExperimentCreate([Body] ExperimentCreateRequest request);

        [Get("/api/Experiment")]
        Task<IList<Experiment>> ExperimentList();

        [Get("/api/Experiment/{idExperiment}")]
        Task<Experiment> ExperimentGet(int idExperiment);

        [Post("/api/Experiment/{idExperiment}/Stop")]
        Task ExperimentStop(int idExperiment);

        [Get("/api/Experiment/{idExperiment}/Rule")]
        Task<IList<DeviceFarmUnitZoneRule>> ExperimentRulesGet(int idExperiment);

        [Post("/api/Experiment/{idExperiment}/Rule")]
        Task<int> ExperimentRuleAdd(int idExperiment, [Body] DeviceFarmUnitZoneRule rule);

        [Delete("/api/Experiment/{idExperiment}/Rule/{idRule}")]
        Task ExperimentRuleDelete(int idExperiment, int idRule);

        [Get("/api/Experiment/{idExperiment}/SensorData")]
        Task<IList<ExperimentSensorSample>> ExperimentSensorDataGet(int idExperiment);

        [Get("/api/Experiment/{idExperiment}/ControllerData")]
        Task<IList<ExperimentControllerEvent>> ExperimentControllerDataGet(int idExperiment);

        [Get("/api/DeviceFarmUnit/Unassigned")]
        Task<IList<DeviceDto>> DeviceUnassignedGet(bool controllerCapable);

        [Post("/api/DeviceFarmUnit/Assign")]
        Task DeviceAssign([Body] DeviceZoneAssignment body);

        [Post("/api/DeviceFarmUnit/Unassign")]
        Task DeviceUnassign(int? idDevice);

        [Get("/api/DeviceFarmUnit/Dashboard")]
        Task<IList<DeviceFarmUnitDashboard>> DeviceFarmUnitDashboardGet();

        [Get("/api/DeviceFarmUnit/Dashboard/Zones")]
        Task<IList<DeviceFarmUnitZoneDashboard>> DeviceFarmUnitZoneDashboardListGet(int? idDeviceFarmUnit);

        [Get("/api/DeviceFarmUnit/Dashboard/Zone")]
        Task<DeviceFarmUnitZoneDashboard> DeviceFarmUnitZoneDashboardGet(int? idDeviceFarmUnitZone);

        [Get("/api/DeviceFarmUnit/Dashboard/Widget")]
        Task<DashboardAggregate> DeviceFarmUnitDashboardWidgetAggregateGet(HierarchyNodeKind level, int levelId);

        // ---- Manual actuate (roadmap #219) ---------------------

        [Post("/api/DeviceFarmUnit/Zone/ManualActuate")]
        Task<IReadOnlyList<int>> DeviceFarmUnitZoneManualActuateStart(int idDeviceFarmUnitZone, [Body] ManualActuateRequest request);

        [Post("/api/DeviceFarmUnit/Unit/ManualActuate")]
        Task<IReadOnlyList<int>> DeviceFarmUnitManualActuateStart(int idDeviceFarmUnit, [Body] ManualActuateRequest request);

        [Post("/api/DeviceFarmUnit/Farm/ManualActuate")]
        Task<IReadOnlyList<int>> DeviceFarmManualActuateStart(int idDeviceFarm, [Body] ManualActuateRequest request);

        [Post("/api/DeviceFarmUnit/Zone/ManualActuate/Stop")]
        Task DeviceFarmUnitZoneManualActuateStop(int idDeviceFarmUnitZone, RelayFunction relayFunction);

        [Get("/api/DeviceFarmUnit/Zone/ManualActuate")]
        Task<IList<DeviceManualOverride>> DeviceFarmUnitZoneManualActuateStatus(int idDeviceFarmUnitZone);

        [Post("/api/FarmOpenfield/Parcel/ManualActuate")]
        Task<IReadOnlyList<int>> ParcelManualActuateStart(int idFarmParcelZone, [Body] ManualActuateRequest request);

        [Post("/api/FarmOpenfield/Parcel/ManualActuate/Stop")]
        Task ParcelManualActuateStop(int idFarmParcelZone, RelayFunction relayFunction);

        [Get("/api/FarmOpenfield/Parcel/ManualActuate")]
        Task<IList<DeviceManualOverride>> ParcelManualActuateStatus(int idFarmParcelZone);

        // ---- Device commands ---------------------------------

        [Post("/api/DeviceCommand")]
        Task<IReadOnlyList<int>> DeviceCommandIssue([Body] IssueCommandRequest request);

        // ---- Discovery ----------------

        [Post("/api/Discovery/Scan")]
        Task<IReadOnlyList<int>> DiscoveryScan([Body] DiscoveryScanRequest request);

        [Get("/api/Discovery/Results")]
        Task<IList<DiscoveryResult>> DiscoveryResultsGet(int? unitID, int? zoneID, int? parcelID = null);

        [Get("/api/Discovery/WifiConfigs")]
        Task<IList<TenantWifiConfig>> DiscoveryWifiConfigsGet();

        [Post("/api/Discovery/WifiConfigs")]
        Task<TenantWifiConfig> DiscoveryWifiConfigAdd([Body] TenantWifiConfig config);

        [Put("/api/Discovery/WifiConfigs/{idTenantWifiConfig}")]
        Task DiscoveryWifiConfigUpdate(int idTenantWifiConfig, [Body] TenantWifiConfig config);

        [Delete("/api/Discovery/WifiConfigs/{idTenantWifiConfig}")]
        Task DiscoveryWifiConfigDelete(int idTenantWifiConfig);

        /// The real Password, unlike DiscoveryWifiConfigsGet() above - see DiscoveryApiController.WifiConfigReveal.
        [Get("/api/Discovery/WifiConfigs/{idTenantWifiConfig}/Reveal")]
        Task<TenantWifiConfig> DiscoveryWifiConfigReveal(int idTenantWifiConfig);

        [Post("/api/Discovery/Register")]
        Task<DiscoveryRegisterResult> DiscoveryRegister([Body] DiscoveryRegisterRequest request);

        // ---- Firmware catalog + per-device update ----

        [Get("/api/Firmware")]
        Task<IList<DeviceFirmware>> FirmwareList(string? board);

        [Post("/api/Firmware/Sync")]
        Task<FirmwareSyncResult> FirmwareSync([Body] FirmwareSyncRequest request);

        [Post("/api/Firmware/Import")]
        Task<FirmwareSyncResult> FirmwareImport([Body] FirmwareImportRequest request);

        [Multipart]
        [Post("/api/Firmware/Upload")]
        Task<DeviceFirmware> FirmwareUpload([AliasAs("file")] StreamPart file);

        [Delete("/api/Firmware")]
        Task FirmwareDelete(int idDeviceFirmware);

        [Multipart]
        [Post("/api/Firmware/UploadZip")]
        Task<FirmwareSyncResult> FirmwareUploadZip([AliasAs("file")] StreamPart file);

        /// A ZIP of the visible catalog + manifest.json, streamed through so the browser download proxies through Agrumy.Web like every other admin action.
        [Get("/api/Firmware/DownloadZip")]
        Task<HttpResponseMessage> FirmwareDownloadZip(bool latestOnly);

        /// Raw .bin bytes of one catalog entry, streamed through the API whatever its source - the Flash Device tab's same-origin path to a GitHub asset.
        [Get("/api/Firmware/Fetch")]
        Task<HttpResponseMessage> FirmwareFetch(string fileName);

        [Post("/api/Device/FirmwareUpdate")]
        Task DeviceFirmwareUpdate([Body] DeviceFirmwareUpdateRequest request);

        [Delete("/api/Device/FirmwareUpdate")]
        Task DeviceFirmwareUpdateCancel(int idDevice);

        [Post("/api/Device/WifiUpdate")]
        Task DeviceWifiUpdate([Body] DeviceWifiUpdateRequest request);

        [Post("/api/DeviceFarmUnit/{idDeviceFarmUnit}/WifiUpdate")]
        Task<UnitWifiUpdateResult> UnitWifiUpdate(int idDeviceFarmUnit, [Body] UnitWifiUpdateRequest request);

        /// GlobalAdmin-only, see DeviceApiController.HardResetRequest - wipes the device on its next reachable poll (normal or, if its apiKey is broken, the apiId-only HardResetPending path).
        [Post("/api/Device/HardReset")]
        Task DeviceHardReset(int idDevice);

        /// Generates a new AES-256 key for LoRa private-protocol uplink encryption, returned once as raw hex - see DeviceApiController.LoRaPrivateKeyGenerate.
        [Post("/api/Device/LoRaPrivateKey/Generate")]
        Task<string> LoRaPrivateKeyGenerate(int idDevice);

        /// Operator-visible "LoRa v2 session" card on Details - see DeviceApiController.LoRaSessionGet; null if the device has never sent an accepted v2 uplink.
        [Get("/api/Device/LoRaSession")]
        Task<DeviceLoRaSessionInfo?> LoRaSessionGet(int idDevice);

        // ---- SensorData ---------------------------------------------------

        [Get("/api/SensorData")]
        Task<string> SensorDataGet(int? deviceID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);

        /// Same JSON shape as SensorDataGet, time-bucket averaged across every device in the zone/unit.
        [Get("/api/SensorData/ZoneAverage")]
        Task<string> SensorDataZoneAverageGet(int deviceFarmUnitZoneID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);

        [Get("/api/SensorData/UnitAverage")]
        Task<string> SensorDataUnitAverageGet(int deviceFarmUnitID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket);

        // ---- Tenant -------------------------------------------------------

        [Get("/api/Tenant/All")]
        Task<IEnumerable<Tenant>> TenantsGet();

        [Get("/api/Tenant")]
        Task<Tenant> TenantGet(int idTenant);

        [Post("/api/Tenant")]
        Task<int> TenantAdd([Body] Tenant tenant);

        [Put("/api/Tenant")]
        Task TenantUpdate([Body] Tenant tenant);

        /// Always the caller's own tenant - see TenantApiController.TenantAlertConfigGet.
        [Get("/api/Tenant/AlertConfig")]
        Task<TenantAlertConfig> TenantAlertConfigGet();

        [Put("/api/Tenant/AlertConfig")]
        Task TenantAlertConfigUpdate([Body] TenantAlertConfig config);

        [Get("/api/Tenant/EmergencyStop")]
        Task<bool> EmergencyStopStatus(int? idTenant = null);

        [Post("/api/Tenant/EmergencyStop")]
        Task EmergencyStopActivate(int? idTenant = null);

        [Post("/api/Tenant/EmergencyStop/Clear")]
        Task EmergencyStopClear(int? idTenant = null);

        /// SENSITIVE (password hashes, device ApiKeys - see TenantApiController.Export) - ZIP bytes, the Web controller streams them straight to the browser, never writing them to disk.
        [Get("/api/Tenant/Export")]
        Task<HttpResponseMessage> TenantExport(int idTenant, bool includeSensorData = false, DateTime? sensorDataSinceUtc = null);

        [Post("/api/Tenant/Import")]
        Task<TenantImportResult> TenantImport([Body] TenantImportRequest value);

        /// Anonymous (see TenantApiController.ImportAsSentinel) - reachable from the same SetupAdmin screen BootstrapPending/BootstrapSetPassword already use.
        [Post("/api/Tenant/ImportAsSentinel")]
        Task<TenantImportResult> TenantImportAsSentinel([Body] TenantExport value);

        // ---- Tenant Quota ---------------------------------------------------

        [Get("/api/TenantQuota")]
        Task<TenantQuota> TenantQuotaGet(int idTenant);

        [Put("/api/TenantQuota")]
        Task TenantQuotaSet([Body] TenantQuota quota);

        // ---- Server config --------------------------------

        [Get("/api/ServerConfig")]
        Task<ServerConfig> ServerConfigGet();

        /// Roadmap #419 - Server Health card, polled on an interval by Agrumy.Web's ServerConfigController.Health (live-refresh.js), not an on-demand button - see ServerConfigApiController.GetHealth.
        [Get("/api/ServerConfig/Health")]
        Task<IReadOnlyList<ServerHealthEntry>> ServerConfigGetHealth();

        /// Sends through the SAVED Email settings, not the unsaved form - see ServerConfigApiController.TestEmail.
        [Post("/api/ServerConfig/TestEmail")]
        Task ServerConfigTestEmail(string toEmail);

        /// Sends through the SAVED Webhook settings, not the unsaved form - see ServerConfigApiController.TestWebhook.
        [Post("/api/ServerConfig/TestWebhook")]
        Task ServerConfigTestWebhook();

        /// Tests the UNSAVED form's archive DB credentials before Update ever persists them - see ServerConfigApiController.TestArchiveDatabase.
        [Post("/api/ServerConfig/TestArchiveDatabase")]
        Task ServerConfigTestArchiveDatabase([Body] ArchiveDbTestRequest request);

        /// The "Data Archiving" subsection's own self-contained save (test-before-persist when enabling) - see ServerConfigApiController.SaveArchiveSettings.
        [Post("/api/ServerConfig/ArchiveSettings")]
        Task ServerConfigSaveArchiveSettings([Body] ArchiveSettingsSaveRequest request);

        [Put("/api/ServerConfig")]
        Task ServerConfigUpdate([Body] ServerConfig config);

        /// The one ServerConfig field an anonymous page (Register) is allowed to read - whether to show the "create a new tenant" option at all.
        [Get("/api/ServerConfig/Public")]
        Task<PublicServerConfig> ServerConfigGetPublic();

        // ---- SsrfGuard allowlists (Firmware and Webhook kept separate) ----

        [Get("/api/ServerConfig/FirmwareSsrfAllowlist")]
        Task<IReadOnlyList<SsrfAllowlistEntry>> FirmwareSsrfAllowlistGet();

        [Post("/api/ServerConfig/FirmwareSsrfAllowlist")]
        Task<SsrfAllowlistEntry> FirmwareSsrfAllowlistAdd([Body] SsrfAllowlistEntry entry);

        [Delete("/api/ServerConfig/FirmwareSsrfAllowlist/{id}")]
        Task FirmwareSsrfAllowlistDelete(int id);

        [Get("/api/ServerConfig/WebhookSsrfAllowlist")]
        Task<IReadOnlyList<SsrfAllowlistEntry>> WebhookSsrfAllowlistGet();

        [Post("/api/ServerConfig/WebhookSsrfAllowlist")]
        Task<SsrfAllowlistEntry> WebhookSsrfAllowlistAdd([Body] SsrfAllowlistEntry entry);

        [Delete("/api/ServerConfig/WebhookSsrfAllowlist/{id}")]
        Task WebhookSsrfAllowlistDelete(int id);

        // ---- Data maintenance -----------------------------

        /// Whether the current DB provider is MariaDB/MySQL - decides whether the Purge confirmation flow needs the extra "shrink files on disk?" dialog at all.
        [Get("/api/DataMaintenance/Provider")]
        Task<DataMaintenanceProviderInfo> DataMaintenanceProviderGet();

        [Post("/api/DataMaintenance/Optimize")]
        Task DataMaintenanceOptimize([Body] DataMaintenanceRequest request);

        [Post("/api/DataMaintenance/Purge")]
        Task DataMaintenancePurge([Body] DataPurgeRequest request);

        [Post("/api/DataMaintenance/PurgeOrphaned")]
        Task DataMaintenancePurgeOrphaned([Body] RecycleBinPurgeRequest request);

        // ---- Audit log --------------------------------------

        [Get("/api/AuditLog")]
        Task<List<AuditLogEntry>> AuditLogGet(int take = 200, string? actorEmail = null, string? action = null, string? targetType = null, DateTime? fromUtc = null, DateTime? toUtc = null);
    }
}

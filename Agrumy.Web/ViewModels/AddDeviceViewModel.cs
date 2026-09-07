namespace api.ViewModels
{
    public class AddDeviceViewModel
    {
        public string? DevicePin { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}

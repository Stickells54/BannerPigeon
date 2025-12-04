using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace BannerPigeon.Settings
{
	public class PigeonSettings : AttributeGlobalSettings<PigeonSettings>
	{
		public override string Id => "BannerPigeon";
		public override string DisplayName => "BannerPigeon";
		public override string FolderName => "BannerPigeon";
		public override string FormatType => "json";

		[SettingPropertyInteger("Pigeon Cost (Gold)", 10, 1000, Order = 0, RequireRestart = false,
			HintText = "Cost in gold to send a carrier pigeon to a lord.")]
		[SettingPropertyGroup("Pigeon Mail", GroupOrder = 0)]
		public int PigeonCost { get; set; } = 50;

		[SettingPropertyInteger("Response Time (Days)", 1, 14, Order = 1, RequireRestart = false,
			HintText = "Number of days before receiving a response from the lord.")]
		[SettingPropertyGroup("Pigeon Mail", GroupOrder = 0)]
		public int ResponseDays { get; set; } = 3;

		[SettingPropertyBool("Enable in Towns", Order = 2, RequireRestart = false,
			HintText = "Allow sending pigeons from towns.")]
		[SettingPropertyGroup("Locations", GroupOrder = 1)]
		public bool EnableInTowns { get; set; } = true;

		[SettingPropertyBool("Enable in Castles", Order = 3, RequireRestart = false,
			HintText = "Allow sending pigeons from castles.")]
		[SettingPropertyGroup("Locations", GroupOrder = 1)]
		public bool EnableInCastles { get; set; } = true;

		[SettingPropertyBool("Show Notifications", Order = 4, RequireRestart = false,
			HintText = "Show notification when a pigeon response arrives.")]
		[SettingPropertyGroup("UI", GroupOrder = 2)]
		public bool ShowNotifications { get; set; } = true;

		[SettingPropertyBool("Use Realistic Travel Time", Order = 5, RequireRestart = false,
			HintText = "Calculate response time based on distance to the lord.")]
		[SettingPropertyGroup("Pigeon Mail", GroupOrder = 0)]
		public bool UseRealisticTravelTime { get; set; } = true;

		[SettingPropertyInteger("Pigeon Speed (Map Units/Day)", 10, 200, Order = 6, RequireRestart = false,
			HintText = "How fast pigeons fly. Higher is faster. Default is 50.")]
		[SettingPropertyGroup("Pigeon Mail", GroupOrder = 0)]
		public int PigeonSpeed { get; set; } = 50;
	}
}

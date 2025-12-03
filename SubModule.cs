using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BannerPigeon
{
	public class SubModule : MBSubModuleBase
	{
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
		{
			base.OnGameStart(game, gameStarterObject);

			try
			{
				if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter)
				{
					CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;
					campaignStarter.AddBehavior(new PigeonPostBehavior());
				}
			}
			catch
			{
				// Silently ignore if not in campaign mode (e.g., Editor)
			}
		}
	}
}

using July.Arch;
using July.Config;
using July.UI;
using cfg;
namespace Game
{
    /// <summary>把 Luban 窗口配置转换为 UI 框架的打开参数，不包含具体界面的业务逻辑。</summary>
    public class LubanUIWindowProvider : IUIWindowProvider
    {
        public bool TryResolve(int windowId, out UIOpenOptions options)
        {
            var configSystem = ArchContext.Current.GetSystem<IConfigSystem>();
            if (configSystem == null || !configSystem.TryGetTable<TbUIWindow>(out var table))
            {
                options = null;
                return false;
            }

            var row = table.GetOrDefault(windowId);
            if (row == null)
            {
                options = null;
                return false;
            }

            options = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(row.Id, row.WindowName),
                Layer = (UILayer)row.UiLayer,
                OpenAnimationType = (UIAnimationType)row.EnterAnimType,
                CloseAnimationType = (UIAnimationType)row.ExitAnimType,
                ShowMask = row.IsNeedBlackMask,
                ClickMaskToClose = row.IsClickBlankQuit,
                IgnoreSafeArea = row.IsIgnoreSafeArea,
            };
            return true;
        }
    }
}

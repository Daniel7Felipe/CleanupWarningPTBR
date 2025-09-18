using System.Threading;
using System.Windows.Controls;
using CleanupWarning.Core;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Utils.General;
using Utils.Torch;

namespace CleanupWarning
{
    public class CleanupWarningPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        CancellationTokenSource _cancellationTokenSource;
        Persistent<CleanupWarningConfig> _config;
        UserControl _userControl;

        public CleanupWarningConfig Config => _config.Data;
        public Core.CleanupWarning Core { get; private set; }

        public UserControl GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.ListenOnGameLoaded(OnGameLoaded);
            this.ListenOnGameUnloading(OnGameUnloading);

            var configPath = this.MakeConfigFilePath();
            _config = Persistent<CleanupWarningConfig>.Load(configPath);
        }

        void OnGameLoaded()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var gridsSearcher = new DeletableGridsSearcher();
            var chatManager = Torch.CurrentSession.Managers.GetManager<IChatManagerServer>();
            Core = new Core.CleanupWarning(Config, chatManager, gridsSearcher);

            TaskUtils.RunUntilCancelledAsync(Core.RunWarningCycles, _cancellationTokenSource.Token).Forget(Log);
        }

        void OnGameUnloading()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
using System.Xml.Serialization;
using Torch;

namespace CleanupWarning
{
    public class CleanupWarningConfig : ViewModel, Core.CleanupWarning.IConfig
    {
        float _interval = 60f;

        [XmlElement]
        public float Interval
        {
            get => _interval;
            set => SetValue(ref _interval, value);
        }
    }
}
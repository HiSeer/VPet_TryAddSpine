using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.SpineSupport4_1
{
    public class SpineSupport4_1 : MainPlugin
    {
        public SpineSupport4_1(IMainWindow mainwin) : base(mainwin)
        {
            VPet_Simulator.Core.PetLoader.IGraphConvert.Add("spine4.1", SpineAnimation.LoadGraph);
        }

        public override string PluginName => "SpineSupport4_1";
    }
}
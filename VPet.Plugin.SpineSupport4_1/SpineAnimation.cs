using LinePutScript;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using VPet_Simulator.Core;

namespace VPet.Plugin.SpineSupport4_1
{
    public class SpineAnimation : IGraph
    {
        public bool PlayState { get; set; } = false;
        public bool IsLoop { get; set; } = false;
        public bool IsContinue { get; set; } = false;

        public bool IsReady { get; private set; } = false;

        public GraphInfo GraphInfo { get; private set; }


        private SkeletonRenderer skeletonRenderer;

        public void Run(Border parant, Action EndAction = null)
        {
            Console.WriteLine(holdAnimation.name);
            if (PlayState)
            {
                IsContinue = true;
                return;
            }
            PlayState = true;
            DoEndAction = true;
            parant.Dispatcher.Invoke(() =>
            {
                System.Windows.Controls.Image img;
                img = (System.Windows.Controls.Image)graphCore.CommUIElements["Image1.Spine"];
                if (img.Parent != parant)
                {
                    if (img.Parent != null)
                    {
                        var lastBorder = (img.Parent as Border);
                        lastBorder.Child = null;
                    }
                    parant.Child = img;
                    parant.Tag = this;
                }
                skeletonRenderer.StartAnimation(img, holdAnimation);

                Task.Run(() =>
                {
                    Thread.Sleep((int)(holdAnimation.duration * 1000));
                    if (IsLoop && PlayState)
                    {
                        Run(parant, EndAction);
                    }
                    else
                    {
                        PlayState = false;
                        if (DoEndAction)
                        {
                            EndAction?.Invoke();//运行结束动画时事件
                        }
                    }
                });
            });
        }

        public void Stop(bool StopEndAction = false)
        {
            PlayState = false;
            this.DoEndAction = !StopEndAction;
        }
        private bool DoEndAction;

        private GraphCore graphCore;
        private Spine.Animation holdAnimation;
        private SpineAnimation(string aniName, GraphCore graphCore, SkeletonRenderer skeletonRenderer, GraphInfo graphinfo, bool isLoop)
        {
            GraphInfo = graphinfo;
            this.graphCore = graphCore;
            this.skeletonRenderer = skeletonRenderer;
            holdAnimation = this.skeletonRenderer.skeleton.Data.FindAnimation(aniName);
            this.IsLoop = isLoop;
            IsReady = true;
        }

        public static void LoadGraph(GraphCore graph, FileSystemInfo path, ILine info)
        {
            if (!graph.CommConfig.ContainsKey("PA_Setup"))
            {
                graph.CommConfig["PA_Setup"] = true;
                graph.CommUIElements["Image1.Spine"] = new System.Windows.Controls.Image() { Height = 500 };
            }

            var skeletonRenderer = new SkeletonRenderer(500, 500, $"{path.FullName}\\{info.info}");
            LpsDocument lps = new LpsDocument(File.ReadAllText(path.FullName + $"\\{info.info}.lps"));
            //遍历line里的信息 生成对应的spineanimation
            foreach (ILine iline in lps)
            {
                string aniName = iline.info;

                var loopSub = iline.Find("loop");
                bool isloop = false;
                if (loopSub != null)
                {
                    isloop = bool.Parse(loopSub.info);
                }
                //var graphType = PraseSubEnum<GraphInfo.GraphType>(iline, "graph");
                Enum.TryParse(iline.Name, out GraphInfo.GraphType graphType);
                var animatType = PraseSubEnum<GraphInfo.AnimatType>(iline, "animat");
                var modeType = PraseSubEnum<GameSave.ModeType>(iline, "mode", GameSave.ModeType.Nomal);

                var graphInfo = new GraphInfo(aniName, graphType, animatType, modeType);
                graph.AddGraph(new SpineAnimation(aniName, graph, skeletonRenderer, graphInfo, isloop));
            }

            //todo 对内置的重启有影响吗 会重启后一直挂着导致gc无法回收或报错吗
            CompositionTarget.Rendering += (object sender, EventArgs e) =>
            {
                skeletonRenderer.Update();
                //Console.WriteLine("CompositionTarget.Rendering" + DateTime.Now.Millisecond);
            };
        }
        public static T PraseSubEnum<T>(ILine line, string sub, T defaultValue = default(T)) where T : struct
        {
            var subValue = line.Find(sub);
            if (subValue == null) return defaultValue;
            if (Enum.TryParse<T>(subValue.info, out T res))
            {
                return res;
            }
            return defaultValue;
        }
    }
}

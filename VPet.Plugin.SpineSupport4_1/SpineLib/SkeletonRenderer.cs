//https://www.bilibili.com/video/BV1Va411m7SD

using Spine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace VPet.Plugin.SpineSupport4_1
{
    public class SkeletonRenderer
    {
        // 自定义TextureLoader
        public class MyTextureLoader : TextureLoader
        {
            public void Load(AtlasPage page, string path)
            {
                BitmapSource imageSource = new BitmapImage(new Uri(path));
                page.rendererObject = imageSource;
            }

            public void Unload(object texture)
            {
                throw new NotImplementedException();
            }
        }

        public Skeleton skeleton { get; private set; }
        private AnimationState state;

        /*
        * 通用属性
        */
        public static int[] QUAD_TRIANGLES = new int[] { 0, 1, 2, 2, 3, 0 };
        public static int VERTEX_SIZE = 2 + 2 + 4;
        private float[] vertices = null;
        // bounds
        private float[] bounds = null;
        // alpha
        private float globalAlpha = 1;

        private int width, height;

        private Dictionary<string, float> tempColor = new Dictionary<string, float>
        {
            { "r", 0 }, { "g", 0 }, { "b", 0 }, { "a", 0 }
        };

        /*
         * 使用 WriteableBitmap 时的属性
         */
        private WriteableBitmap wBitmap = null;
        private Bitmap backBitmap = null;
        private Graphics graphics = null;

        private Bitmap sourceBitmap = null;
        private ImageAttributes attributes = null;
        private float[][] colorArray ={ new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}};
        private ColorMatrix colorMatrix = null;
        private System.Drawing.Drawing2D.GraphicsPath p = null;

        // 初始化，使用WriteableBitmap
        public SkeletonRenderer(int _width, int _height, string pathWithFileName)
        {
            width = _width;
            height = _height;
            // 初始化网格列表
            vertices = NewFloatArray(8 * 1024);

            attributes = new ImageAttributes();
            colorMatrix = new ColorMatrix(colorArray);
            p = new System.Drawing.Drawing2D.GraphicsPath();

            wBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            backBitmap = new Bitmap(width, height, wBitmap.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, wBitmap.BackBuffer);
            graphics = Graphics.FromImage(backBitmap);
            sourceBitmap = new Bitmap($"{pathWithFileName}.png");

            graphics.TranslateTransform(width / 2, height);
            graphics.Clear(System.Drawing.Color.Transparent);

            //this.image.Source = wBitmap;

            LoadSkleleton(pathWithFileName);
        }

        public void LoadSkleleton(string pathWithFileName)
        {
            // 加载图集
            Atlas atlas = new Atlas($"{pathWithFileName}.atlas", new MyTextureLoader());

            // 加载骨架数据
            SkeletonData skeletonData;
            if (File.Exists($"{pathWithFileName}.json"))
            {
                SkeletonJson json = new SkeletonJson(atlas);
                skeletonData = json.ReadSkeletonData($"{pathWithFileName}.json");
            }
            else
            {
                SkeletonBinary binary = new SkeletonBinary(atlas);
                skeletonData = binary.ReadSkeletonData($"{pathWithFileName}.skel");
            }

            // 设置动画状态和混合时间
            AnimationStateData animationStateData = new AnimationStateData(skeletonData);
            animationStateData.defaultMix = 0.1f;
            state = new AnimationState(animationStateData);

            // 设置skeleton
            skeleton = new Skeleton(skeletonData);
            skeleton.ScaleY = -1f;
            skeleton.SetToSetupPose();
            skeleton.UpdateWorldTransform();
            // 如果对象上没有bounds数据，获取bounds数据
            bounds = CalculateBounds(skeleton);
        }

        public void StartAnimation(System.Windows.Controls.Image image, string aniName)
        {
            state.SetAnimation(0, aniName, true);
            StartAnimation(image);
        }
        public void StartAnimation(System.Windows.Controls.Image image, Spine.Animation animation)
        {
            state.SetAnimation(0, animation, true);
            StartAnimation(image);
        }
        private void StartAnimation(System.Windows.Controls.Image image)
        {
            image.Source = wBitmap;

            // 应用动画
            state.Update(0);
            state.Apply(skeleton);
            // 获取当前时间
            lastFrameTime = DateTime.Now;
        }

        private DateTime lastFrameTime;
        public void Update()
        {
            // 获取时间
            DateTime now = DateTime.Now;
            TimeSpan delta = now - lastFrameTime;
            if (delta.Milliseconds > 20)
            {
                // 应用动画
                state.Update((float)delta.TotalMilliseconds / 1000);
                state.Apply(skeleton);
                skeleton.UpdateWorldTransform();

                // 测试渲染
                Draw(skeleton);
                lastFrameTime = now;
            }
        }

        private void Draw(Skeleton skeleton)
        {
            if (graphics != null)
            {
                DrawTrianglesGraphics(skeleton);
            }

        }

        // 获取bounds数据
        private float[] CalculateBounds(Skeleton skeleton)
        {
            float[] v = { };
            skeleton.GetBounds(out float x, out float y, out float width, out float height, ref v);
            return new float[] { x, y, width, height };
        }

        private void DrawTrianglesGraphics(Skeleton skeleton)
        {
            // WriteableBitmap 锁定(锁定才能设定DirtyRect)
            wBitmap.Lock();
            // 在WriteableBitmap锁定之后设置屏幕背景颜色为透明
            graphics.Clear(System.Drawing.Color.Transparent);
            // 遍历插槽
            int[] triangles;
            foreach (Slot slot in skeleton.drawOrder)
            {
                // 初始化变量
                float r = 1, g = 1, b = 1;
                Attachment attachment = slot.attachment;
                AtlasRegion region;
                if (attachment is RegionAttachment regionAttachment)
                {
                    vertices = ComputeRegionVertices(slot, regionAttachment, false);
                    triangles = QUAD_TRIANGLES;
                    region = (AtlasRegion)regionAttachment.Region;

                    r = slot.bone.skeleton.r * slot.r * regionAttachment.r;
                    g = slot.bone.skeleton.g * slot.g * regionAttachment.g;
                    b = slot.bone.skeleton.b * slot.b * regionAttachment.b;
                    // 设置alpha
                    globalAlpha = slot.bone.skeleton.a * slot.a * regionAttachment.a;

                    colorMatrix.Matrix33 = globalAlpha;
                    attributes.SetColorMatrix(colorMatrix);
                }
                else if (attachment is MeshAttachment meshAttachment)
                {
                    vertices = ComputeMeshVertices(slot, meshAttachment, false);
                    triangles = meshAttachment.triangles;
                    region = (AtlasRegion)meshAttachment.Region;
                    r = slot.bone.skeleton.r * slot.r * meshAttachment.r;
                    g = slot.bone.skeleton.g * slot.g * meshAttachment.g;
                    b = slot.bone.skeleton.b * slot.b * meshAttachment.b;
                    // 设置alpha
                    globalAlpha = slot.bone.skeleton.a * slot.a * meshAttachment.a;

                    colorMatrix.Matrix33 = globalAlpha;
                    attributes.SetColorMatrix(colorMatrix);
                }
                else
                {
                    continue;
                }

                // 开始绘制
                if (region != null)
                {
                    if (r != 1 || g != 1 || b != 1 || globalAlpha != 1)
                    {
                        if (globalAlpha == 0)
                        {
                            break;
                        }
                    }


                    for (var i = 0; i < triangles.Length; i += 3)
                    {
                        int t1 = triangles[i] * 8, t2 = triangles[i + 1] * 8, t3 = triangles[i + 2] * 8;

                        float x0 = vertices[t1], y0 = vertices[t1 + 1], u0 = vertices[t1 + 6], v0 = vertices[t1 + 7];
                        float x1 = vertices[t2], y1 = vertices[t2 + 1], u1 = vertices[t2 + 6], v1 = vertices[t2 + 7];
                        float x2 = vertices[t3], y2 = vertices[t3 + 1], u2 = vertices[t3 + 6], v2 = vertices[t3 + 7];

                        this.DrawTriangleGraphics(sourceBitmap, x0, y0, u0, v0, x1, y1, u1, v1, x2, y2, u2, v2);


                    }

                }
            }
            //graphics.DrawImage(sourceBitmap, 0, 0);

            // 设置 DirtyRect
            wBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));

            wBitmap.Unlock();
        }

        // 初始化数组方法
        private float[] NewFloatArray(int size)
        {
            float[] result = new float[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = 0;
            }
            return result;
        }

        // 计算顶点的方法
        private float[] ComputeRegionVertices(Slot slot, RegionAttachment region, bool pma)
        {
            Skeleton skeleton = slot.bone.skeleton;
            // 计算alpha通道
            float alpha = skeleton.a * slot.a * region.a;
            float multiplier = pma ? alpha : 1;
            // 设置颜色
            Dictionary<string, float> color = this.tempColor;
            color["r"] = skeleton.r * slot.r * region.r * multiplier;
            color["g"] = skeleton.g * slot.g * region.g * multiplier;
            color["b"] = skeleton.b * slot.b * region.b * multiplier;
            color["a"] = alpha;
            // 调用RegionAttachment中的方法计算顶点
            region.ComputeWorldVertices(slot, this.vertices, 0, VERTEX_SIZE);

            // 引入本地
            float[] vertices = this.vertices;
            float[] uvs = region.uvs;

            vertices[2] = color["r"];
            vertices[3] = color["g"];
            vertices[4] = color["b"];
            vertices[5] = color["a"];
            vertices[6] = uvs[0];
            vertices[7] = uvs[1];

            vertices[10] = color["r"];
            vertices[11] = color["g"];
            vertices[12] = color["b"];
            vertices[13] = color["a"];
            vertices[14] = uvs[2];
            vertices[15] = uvs[3];

            vertices[18] = color["r"];
            vertices[19] = color["g"];
            vertices[20] = color["b"];
            vertices[21] = color["a"];
            vertices[22] = uvs[4];
            vertices[23] = uvs[5];

            vertices[26] = color["r"];
            vertices[27] = color["g"];
            vertices[28] = color["b"];
            vertices[29] = color["a"];
            vertices[30] = uvs[6];
            vertices[31] = uvs[7];

            return vertices;
        }


        private float[] ComputeMeshVertices(Slot slot, MeshAttachment mesh, bool pma)
        {
            Skeleton skeleton = slot.bone.skeleton;
            // 计算alpha通道
            float alpha = skeleton.a * slot.a * mesh.a;
            float multiplier = pma ? alpha : 1;
            // 设置颜色
            Dictionary<string, float> color = this.tempColor;
            color["r"] = skeleton.r * slot.r * mesh.r * multiplier;
            color["g"] = skeleton.g * slot.g * mesh.g * multiplier;
            color["b"] = skeleton.b * slot.b * mesh.b * multiplier;
            color["a"] = alpha;


            float[] vertices = this.vertices;

            int numVertices = mesh.worldVerticesLength / 2;
            // 重新生成适合MeshAttachment长度的vertices数组
            if (vertices.Length < mesh.worldVerticesLength)
            {
                vertices = NewFloatArray(mesh.worldVerticesLength);
            }

            // 调用MeshAttachment中的方法计算顶点
            mesh.ComputeWorldVertices(slot, 0, mesh.worldVerticesLength, vertices, 0, VERTEX_SIZE);

            // 引入本地
            float[] uvs = mesh.uvs;
            // 写入顶点数据
            for (int i = 0, u = 0, v = 2; i < numVertices; i++)
            {
                vertices[v++] = color["r"];
                vertices[v++] = color["g"];
                vertices[v++] = color["b"];
                vertices[v++] = color["a"];
                vertices[v++] = uvs[u++];
                vertices[v++] = uvs[u++];
                v += 2;
            }


            return vertices;
        }

        // 绘制三角形Graphics
        private void DrawTriangleGraphics(Bitmap imageSource, float x0, float y0,
            float u0, float v0, float x1, float y1, float u1, float v1,
            float x2, float y2, float u2, float v2)
        {
            u0 *= imageSource.Width;
            v0 *= imageSource.Height;
            u1 *= imageSource.Width;
            v1 *= imageSource.Height;
            u2 *= imageSource.Width;
            v2 *= imageSource.Height;

            System.Drawing.Drawing2D.GraphicsState graphicsState = graphics.Save();
            // 设置裁剪区域

            p.AddPolygon(new PointF[] { new PointF(x0, y0), new PointF(x1, y1), new PointF(x2, y2) });
            p.CloseFigure();
            x1 -= x0;
            y1 -= y0;
            x2 -= x0;
            y2 -= y0;

            u1 -= u0;
            v1 -= v0;
            u2 -= u0;
            v2 -= v0;

            float det = 1 / (u1 * v2 - u2 * v1);

            // 线性变换
            float a = (v2 * x1 - v1 * x2) * det;
            float b = (v2 * y1 - v1 * y2) * det;
            float c = (u1 * x2 - u2 * x1) * det;
            float d = (u1 * y2 - u2 * y1) * det;

            if (a == 0)
            {
                return;
            }

            float e = x0 - a * u0 - c * v0;
            float f = y0 - b * u0 - d * v0;

            // 仿射变换
            System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix(a, b, c, d, e, f);

            graphics.MultiplyTransform(matrix);

            // 求逆矩阵（计算出在image中的clip坐标）
            matrix.Invert();
            p.Transform(matrix);
            // 裁剪出合适的位置
            graphics.SetClip(p);

            // 绘制图像（使用透明度）
            //graphics.DrawImage(sourceBitmap,
            //    sourceRectangle,
            //    0, 0, sourceBitmap.Width, sourceBitmap.Height,
            //    GraphicsUnit.Pixel,
            //    attributes);
            // 绘制图像（不使用透明度）  
            graphics.DrawImage(sourceBitmap, 0, 0);

            graphics.Restore(graphicsState);

            p.Reset();
        }
    }
}

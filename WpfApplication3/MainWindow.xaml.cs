using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApplication3
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Kinect初始化：发现Kinect、注册状态变化事件、卸载Kinect方法
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) => DiscoverKinectSensor();
            this.Unloaded += (s, e) => this.kinect = null;
        }

        private KinectSensor kinect;
        public KinectSensor Kinect
        {
            get { return this.kinect; }
            set
            {
                if (this.kinect != value) //强制复制，覆盖原来的Object
                {
                    if (this.kinect != null)
                    {
                        this.kinect = null;
                        UninitializeKinectSensor(this.kinect);
                    }
                    if (value != null && value.Status == KinectStatus.Connected)
                    {
                        this.kinect = value;
                        InitializeKinectSensor(this.kinect);
                    }
                }
            }
        }

        private void DiscoverKinectSensor()
        {
            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
            //程序启动时，如果Kinect未连接，这里this.Kinect是空
            this.Kinect = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
        }
        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case KinectStatus.Connected:
                    if (this.kinect == null)
                        this.kinect = e.Sensor;
                    break;
                case KinectStatus.Disconnected:
                    if (this.kinect == e.Sensor)
                    {
                        this.kinect = null;
                        this.kinect = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);//如果当前Kinect断开了，则找别的设备
                        if (this.kinect == null)
                        {
                            //TODO:通知用于Kinect已拔出
                        }
                    }
                    break;
                    //TODO:处理其他情况下的状态
            }
        }
        private void UninitializeKinectSensor(KinectSensor kinectSensor)
        {
            if (kinectSensor != null)
            {
                kinectSensor.Stop();
                kinectSensor.DepthFrameReady -= kinectSensor_DepthFrameReady;
            }
        }
        #endregion

        //----性能优化点1：使用WriteableBitmap，避免重复创建的开销----
        private WriteableBitmap writableBitMapDepthImage;
        private Int32Rect depthImageBitmapRect;
        private int depthImageStride; //
        private ImageMergeProvider imageMerge;
        //------------------------------------------------------------
        private void InitializeKinectSensor(KinectSensor kinectSensor)
        {//此方法只会在KinectReady的时候被调用一次，用于初始化
            if (kinectSensor != null)
            {
                //获取深度数据流的引用
                DepthImageStream depthStream = kinectSensor.DepthStream;
                depthStream.Enable();

                writableBitMapDepthImage = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Gray16, null);
                depthImageBitmapRect = new Int32Rect(0, 0, depthStream.FrameWidth, depthStream.FrameHeight);
                //跨行的步长（字节）：深度图像宽度多少像素*每像素多少字节
                depthImageStride = depthStream.FrameWidth * depthStream.FrameBytesPerPixel;
                //指定窗体上的图像显示框的数据源
                ColorImageElement.Source = writableBitMapDepthImage;
                kinectSensor.DepthFrameReady += kinectSensor_DepthFrameReady; //每一帧调用一次
                kinectSensor.Start();
                imageMerge = new ImageMergeProvider(depthStream.FrameWidth * depthStream.FrameHeight,10);
            }
        }
        void kinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                LabelPixelData.Content = AppInfoHelper.LoopCallToGetCallsPerSecond();
                if (depthFrame != null)
                {
                    short[] depthPixelDatas = new short[depthFrame.PixelDataLength];
                    depthFrame.CopyPixelDataTo(depthPixelDatas);
                    
                    if (catchFlag)
                    {
                        var result = imageMerge.Merge(depthPixelDatas);
                        if(result!=null)
                        {//把图片叠加后的结果显示在控件上
                            writableBitMapDepthImage.WritePixels(depthImageBitmapRect, result, depthImageStride, 0);
                            catchFlag = false;
                            SaveImageFile();
                        }
                        
                    }
                    writableBitMapDepthImage.WritePixels(depthImageBitmapRect, depthPixelDatas, depthImageStride, 0);

                }

            }
        }

        bool catchFlag = false;
        private void MergePictureButton_Click(object sender, RoutedEventArgs e)
        {
            catchFlag = true;
            //if(catchFlag)
            //{
            //    catchFlag = false;
            //    BtnMerge.Content = "点击开始截图";
            //}
            //else
            //{
            //    catchFlag = true;
            //    BtnMerge.Content = "正在截图。点击结束。。";
            //}
        }
        private void TakePictureButton_Click(object sender, RoutedEventArgs e)
        {
            SaveImageFile();
        }
        private void SaveImageFile()
        {
            String fileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "snapshot.jpg";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (FileStream savedSnapshot = new FileStream(fileName, FileMode.CreateNew))
            {
                BitmapSource image = (BitmapSource)ColorImageElement.Source;
                BmpBitmapEncoder imageEncoder = new BmpBitmapEncoder();
                //JpegBitmapEncoder imageEncoder = new JpegBitmapEncoder();
                //imageEncoder.QualityLevel = 70;
                imageEncoder.Frames.Add(BitmapFrame.Create(image));
                imageEncoder.Save(savedSnapshot);

                savedSnapshot.Flush();
                savedSnapshot.Close();
                savedSnapshot.Dispose();
            }
        }



        public int NormalizeTo16Bit(int lower, int upper, int source)
        {
            //指定上下限，让source在上下限的情况投射到16位数值空间中
            if (source > upper || source < lower)
                return -1;
            return (int)((source - lower) * Int16.MaxValue / (upper - lower));
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            catchFlag = true;
        }
    }




    public static class Extensions
    {
        public static int GetRealDistance(this short source)
        {
            return source >> DepthImageFrame.PlayerIndexBitmaskWidth;
        }
    }
    
    /// <summary>
    /// 图片叠加器：可以在构造时，指定叠加次数。重复调用其叠加方法，当达到指定的叠加次数时，返回叠加后的图片，叠加计数清零。其他时候返回null
    /// </summary>
    public class ImageMergeProvider
    {
        public short[] MergedResult { get; private set; }
        public int Times { get; private set; }
        public ImageMergeProvider(int length, int times)
        {
            //初始化为全-1的数组
            MergedResult = Enumerable.Repeat<short>(-1, length).ToArray();
            //指定叠加次数
            Times = times;
        }
        int currentTime = 0;
        public short[] Merge(short[] source)
        {
            currentTime++;
            if(currentTime<Times)  //如果当前次数小于规定次数
            {
                //MergeToResult(source);
                MergeWithTime(source, currentTime);
                return null;
            }
            else
            {
                //输出图像
                return MergedResult;
            }
        }
        private short[] MergeWithTime(short[] source, int weight)
        {
            int max = Math.Max(source.Length, MergedResult.Length);
            for (int i = 0; i < max; i++)
            {
                if (source[i] > 0)
                {
                    if (MergedResult[i] > 0)
                    {
                        MergedResult[i] = (short)((source[i] + (weight - 1) * MergedResult[i]) / weight);
                        //均值
                        //MergedResult[i] = (short)((source[i] + MergedResult[i]) / 2);
                    }
                    else
                    {
                        MergedResult[i] = source[i];
                    }
                }
            }
            return MergedResult;
        }
        private short[] MergeToResult(short[] source) 
        {
            int max = Math.Max(source.Length, MergedResult.Length);
            for(int i=0;i<max;i++)
            {
                if (source[i] > 0)
                {
                    if (MergedResult[i] > 0)
                    {
                        //均值
                        MergedResult[i] = (short)((source[i] + MergedResult[i]) / 2);
                    }
                    else
                    {
                        MergedResult[i] = source[i];
                    }
                }
            }
            return MergedResult;
        }
    }
    

}

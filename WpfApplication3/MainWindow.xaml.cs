using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private WriteableBitmap depthImageBitMap;
        private Int32Rect depthImageBitmapRect;
        private Int32Rect depthImageNormalizeBitmapRect;
        private int depthImageStride; //
        //------------------------------------------------------------
        private void InitializeKinectSensor(KinectSensor kinectSensor)
        {
            if (kinectSensor != null)
            {
                DepthImageStream depthStream = kinectSensor.DepthStream;
                depthStream.Enable();

                depthImageBitMap = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Gray16, null);
                depthImageBitmapRect = new Int32Rect(0, 0, depthStream.FrameWidth, depthStream.FrameHeight);
                depthImageStride = depthStream.FrameWidth * depthStream.FrameBytesPerPixel;

                ColorImageElement.Source = depthImageBitMap;
                kinectSensor.DepthFrameReady += kinectSensor_DepthFrameReady;
                kinectSensor.Start();
            }
        }
        void kinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    short[] depthPixelDate = new short[depthFrame.PixelDataLength];
                    depthFrame.CopyPixelDataTo(depthPixelDate);
                    
                    depthImageBitMap.WritePixels(depthImageBitmapRect, depthPixelDate, depthImageStride, 0);
                }
            }
        }

        

        private void TakePictureButton_Click(object sender, RoutedEventArgs e)
        {
            String fileName = DateTime.Now.ToString("yyyyMMddHHmmss")+"snapshot.jpg";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            using (FileStream savedSnapshot = new FileStream(fileName, FileMode.CreateNew))
            {
                BitmapSource image = (BitmapSource)ColorImageElement.Source;
                JpegBitmapEncoder jpgEncoder = new JpegBitmapEncoder();
                jpgEncoder.QualityLevel = 70;
                jpgEncoder.Frames.Add(BitmapFrame.Create(image));
                jpgEncoder.Save(savedSnapshot);

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
    }


}

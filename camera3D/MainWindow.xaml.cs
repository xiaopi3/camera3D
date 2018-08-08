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

using SpinnakerNET;
using SpinnakerNET.GenApi;
using SpinnakerNET.GUI;
using System.Windows.Forms;
using System.Drawing;

using System.Windows.Interop;
using System.Runtime.InteropServices;

using Emgu.CV;
using Emgu.CV.CvEnum;

using System.Threading;

namespace camera3D
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private int NumImages;
        private String SavePath=null;
        private String deviceSerialNumber;
        
        private enum AcquireMode { CONTINUOUS,MULTIFRAME,SINGLEFRAME};

        private ManagedSystem system;
        private IList<IManagedCamera> camList;
        private IManagedCamera cam;

        System.Diagnostics.Stopwatch stopwatch1 = new System.Diagnostics.Stopwatch();
        System.Diagnostics.Stopwatch stopwatch2 = new System.Diagnostics.Stopwatch();

        System.Windows.Forms.Timer time = new System.Windows.Forms.Timer();

        public MainWindow()
        {
            InitializeComponent();

            
            time.Tick += new EventHandler(Time_Tick);
            time.Interval = 10;

            exposureControl.sliderName.Content = "曝光时间";
            exposureControl.sliderCheck.Content = "手动曝光";

            frameRateControl.sliderName.Content = "帧率";
            frameRateControl.sliderCheck.Content = "手动帧率";

            blackLevelControl.sliderName.Content = "灰度偏移";
            blackLevelControl.sliderCheck.Content = "手动偏移";

            GainControl.sliderName.Content = "增益";
            GainControl.sliderCheck.Content = "手动增益";

            if (SavePath == null)
            {
                SavePath = "./";
            }
            FileStream fileStream;
            try
            {
                fileStream = new FileStream(SavePath + "test.txt", FileMode.Create);
                fileStream.Close();
                File.Delete("test.txt");
            }
            catch
            {
                Console.WriteLine("权限不足");
                return;
            }

            // 单例
            system = new ManagedSystem();
            
            // 输出当前库版本
            LibraryVersion spinVersion = system.GetLibraryVersion();
            Console.WriteLine("Spinnaker library version: {0}.{1}.{2}.{3}\n\n",
                              spinVersion.major,
                              spinVersion.minor,
                              spinVersion.type,
                              spinVersion.build);
            //输出相机参数-0
            camList = system.GetCameras();
            if (camList.Count == 0)
            {
                camList.Clear();
                system.Dispose();
                Console.WriteLine("无相机！");
                //System.Windows.Application.Current.Shutdown();
                return;
            }
            cam = camList[0];
            INodeMap nodeMapTLDevice = cam.GetTLDeviceNodeMap();
            PrintDeviceInfo(nodeMapTLDevice);

            // 为文件名检索设备序列号
            // *** NOTES ***
            // 设备序列号可以放置其他设备覆盖本设备图像 
            // 图像ID和帧ID
            //
            deviceSerialNumber = "";
            while (true)
            {
                try
                {
                    cam.Init();
                    break;
                }
                catch(SpinnakerException e)
                {
                    Console.WriteLine("init error");
                    Thread.Sleep(1000);
                }
            }
            while (cam.DeviceIndicatorMode.Value != DeviceIndicatorModeEnums.Active.ToString())
            {
                Thread.Sleep(1000);
            }

            
            //相机参数初始化
            cam.ExposureAuto.Value = ExposureAutoEnums.Continuous.ToString();
            cam.AcquisitionFrameRateEnable.Value = true;
            frameRateManualDisable();
            //cam.BlackLevelAuto.Value = BlackLevelAutoEnums.Continuous.ToString();
            cam.GainAuto.Value = GainAutoEnums.Continuous.ToString();

            Console.WriteLine("设备序列号为： {0}", cam.DeviceSerialNumber.Value);
            Console.WriteLine("曝光时间设置为 {0} us", cam.ExposureTime.Value);
            cam.DeviceLinkThroughputLimit.Value = cam.DeviceLinkThroughputLimit.Max;
            Console.WriteLine("当前带宽：{0}", cam.DeviceLinkThroughputLimit.Value);
            Console.WriteLine("当前帧率：{0} Hz", cam.AcquisitionFrameRate.Value);
#if DEBUG
            // 判断是否禁用了心跳检测
            if (DisableHeartbeat() != 0)
            {
                Console.WriteLine("心跳检测未禁用");
                System.Windows.Application.Current.Shutdown();
            }
#endif
            


            //调用刷新控件
            flushControl();
            //控件委托
            #region
            exposureControl.setSliderEvent += () => { cam.ExposureTime.Value = exposureControl.slider.Value; };
            exposureControl.setCheckedEvent += () => { cam.ExposureAuto.Value = ExposureAutoEnums.Off.ToString(); };
            exposureControl.setUnCheckEvent += () => { cam.ExposureAuto.Value = ExposureAutoEnums.Continuous.ToString(); };

            frameRateControl.setSliderEvent += () => { cam.AcquisitionFrameRate.Value = frameRateControl.slider.Value; };
            frameRateControl.setCheckedEvent += frameRateManualEnable;
            frameRateControl.setUnCheckEvent += frameRateManualDisable;

            blackLevelControl.setSliderEvent += () => { cam.BlackLevelAuto.Value = BlackLevelAutoEnums.Off.ToString(); };
            blackLevelControl.slider.IsEnabled = false;

            GainControl.setSliderEvent += () => { cam.Gain.Value = GainControl.slider.Value; };
            GainControl.setCheckedEvent += () => { cam.GainAuto.Value = GainAutoEnums.Off.ToString(); };
            GainControl.setUnCheckEvent += () => { cam.GainAuto.Value = GainAutoEnums.Continuous.ToString(); };
            #endregion

        }

        private void Time_Tick(object sender, EventArgs e)
        {
            Hz.Content = Math.Round(cam.AcquisitionFrameRate.Value, 2).ToString();
        }

        private void frameRateManualDisable()
        {
            IEnum iAcquisitionFrameRateAuto = cam.GetNodeMap().GetNode<IEnum>("AcquisitionFrameRateAuto");
            if (iAcquisitionFrameRateAuto == null || !iAcquisitionFrameRateAuto.IsWritable)
            {
                Console.WriteLine("无法设置帧率模式\n");
                return;
            }

            IEnumEntry iAcquisitionFrameRateAutoOff = iAcquisitionFrameRateAuto.GetEntryByName("Continuous");
            if (iAcquisitionFrameRateAutoOff == null || !iAcquisitionFrameRateAutoOff.IsReadable)
            {
                Console.WriteLine("无法设置启用帧率自动模式\n");
                return;
            }
            // Set symbolic from entry node as new value for enumeration node
            iAcquisitionFrameRateAuto.Value = iAcquisitionFrameRateAutoOff.Symbolic;
        }

        private void frameRateManualEnable()
        {
            // 启动手动帧率
            IEnum iAcquisitionFrameRateAuto = cam.GetNodeMap().GetNode<IEnum>("AcquisitionFrameRateAuto");
            if (iAcquisitionFrameRateAuto == null || !iAcquisitionFrameRateAuto.IsWritable)
            {
                Console.WriteLine("无法设置帧率模式\n");
                return;
            }

            IEnumEntry iAcquisitionFrameRateAutoOff = iAcquisitionFrameRateAuto.GetEntryByName("Off");
            if (iAcquisitionFrameRateAutoOff == null || !iAcquisitionFrameRateAutoOff.IsReadable)
            {
                Console.WriteLine("无法设置禁用帧率自动模式\n");
                return;
            }
            // Set symbolic from entry node as new value for enumeration node
            iAcquisitionFrameRateAuto.Value = iAcquisitionFrameRateAutoOff.Symbolic;
        }

        //刷新控件数值显示
        void flushControl()
        {
            exposureControl.slider.Maximum = cam.ExposureTime.Max;
            exposureControl.slider.Minimum = cam.ExposureTime.Min;
            exposureControl.slider.Value = cam.ExposureTime.Value;

            frameRateControl.slider.Maximum = cam.AcquisitionFrameRate.Max;
            frameRateControl.slider.Minimum = cam.AcquisitionFrameRate.Min;
            frameRateControl.slider.Value = cam.AcquisitionFrameRate.Value;

            blackLevelControl.slider.Maximum = cam.BlackLevel.Max;
            blackLevelControl.slider.Minimum = cam.BlackLevel.Min;
            blackLevelControl.slider.Value = cam.BlackLevel.Value;

            GainControl.slider.Maximum = cam.Gain.Max;
            GainControl.slider.Minimum = cam.Gain.Min;
            GainControl.slider.Value = cam.Gain.Value;
        }

#if DEBUG
        // debug时，在GEV相机上禁用心跳检测，防止超时异常
        int DisableHeartbeat()
        {
            int result = 0;

            //Console.WriteLine("检测目标设备判断是否可以设置心跳检测...\n\n");

            //
            // 给bool控制节点置位来开关相机心跳检测
            // 
            // *** NOTES ***
            // 只在 DEBUG 模式启动
            // 调试的时候GEV相机的内置心跳检测会导致超时，禁用心跳检测能让调试正常进行。
            //
            // *** LATER ***
            // 调试完，请重启相机，恢复心跳检测
            // 

            // 确定目标设备位GEV相机
            try
            {
                //Console.WriteLine("GigE设备正在运行，尝试禁用心跳检测...\n\n");
                cam.GevGVCPHeartbeatDisable.Value = true;
                //Console.WriteLine("警告： 在当前DEBUG模式关闭心跳检测");
                //Console.WriteLine("       启用心跳检测请重启设备...");
            }
            catch (Exception)
            {
                Console.WriteLine("心跳检测设置失败...");
                return -1;
            }
            return result;
        }
#endif        
        void PrintDeviceInfo(INodeMap nodeMap)
        {
            try
            {
                Console.WriteLine("\n*** DEVICE INFORMATION ***\n");
                ICategory category = nodeMap.GetNode<ICategory>("DeviceInformation");
                if (category != null && category.IsReadable)
                {
                    for (int i = 0; i < category.Children.Length; i++)
                    {
                        Console.WriteLine("{0}: {1}", category.Children[i].Name, (category.Children[i].IsReadable ? category.Children[i].ToString() : "Node not available"));
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("设备控制信息无法获取");
                }
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }
        // 主体
        void RunSingleCamera(AcquireMode acquireMode)
        {
            
            try
            {
                // 获取图像
                AcquireImages(acquireMode);
                
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }
        void AcquireImages(AcquireMode acquireMode)
        {
            try
            {
                try
                {
                    if (acquireMode.Equals(AcquireMode.CONTINUOUS))
                    {
                        cam.AcquisitionMode.Value = AcquisitionModeEnums.Continuous.ToString();
                    }
                    else if (acquireMode.Equals(AcquireMode.MULTIFRAME))
                    {
                        cam.AcquisitionMode.Value = AcquisitionModeEnums.MultiFrame.ToString();
                        cam.AcquisitionFrameCount.Value = NumImages;
                    }
                    else if (acquireMode.Equals(AcquireMode.SINGLEFRAME))
                    {
                        cam.AcquisitionMode.Value = AcquisitionModeEnums.SingleFrame.ToString();
                    }
                    //Console.WriteLine("设置获取模式-成功！");
                }
                catch (Exception)
                {
                    Console.WriteLine("连续获取模式设置-失败！");
                }

                //
                // 获取图像前
                //
                // *** NOTES ***
                // 获取图像前的操作取决于 图像获取模式。
                // Single frame captures ：单图模式只获取一张图像
                // multi frame catures ：多图模式获取多张图像
                // continuous captures ：连续模式获得图像流
                // 
                // *** LATER ***
                // 不需要获取图像后需要关闭图像获取
                //
                cam.BeginAcquisition();

                //Console.WriteLine("获取图像...");

                switch (acquireMode)
                {
                    case AcquireMode.CONTINUOUS:
                        stopwatch2.Start();
                        int miss = 0;
                        for (int imageCnt = 0; imageCnt < 100; imageCnt++)
                        {
                            try
                            {
                                //
                                // 获取下一幅图像
                                //
                                // *** NOTES ***
                                // 获取相机缓冲器上的图像，若图像不存在则导致相机挂起
                                //
                                // 使用using关键字可以保证图像正确释放
                                // 缓冲器图像超出容量导致相机挂起，调用 Release() 可以手动释放图像。
                                // 
                                using (IManagedImage rawImage = cam.GetNextImage())
                                {
                                    //
                                    // 确保图像的完整性
                                    //
                                    // *** NOTES ***
                                    // 检测完整性，并检测错误
                                    //
                                    if (rawImage.IsIncomplete)
                                    {
                                        miss++;
                                        Console.WriteLine("图像不完整，状态为： {0}...", rawImage.ImageStatus);
                                        Console.WriteLine("{1} miss={0}", miss,imageCnt);
                                    }
                                    else
                                    {
                                        //
                                        // 输出长宽
                                        //
                                        // *** NOTES ***
                                        // 图像包含大量元数据，包括CRC、图像状态和偏移值等等
                                        //
                                        //uint width = rawImage.Width;

                                        //uint height = rawImage.Height;

                                        //Console.WriteLine("当前图像 {0}, width = {1}, height = {2}", imageCnt, width, height);

                                        //
                                        // 转换为8位单通道
                                        //
                                        // *** NOTES ***
                                        // 图像格式可以在已有枚举间任意转换
                                        // 与原始图像不同，转换的图像无需释放且不影响图像缓冲器
                                        // 
                                        // using避免内存溢出.
                                        //
                                        using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.Mono8))
                                        {
                                            //显示
                                            //ImageBox.Source = ToBitmapSource(convertedImage.bitmap);
                                            
                                        }
                                    }
                                }
                            }
                            catch (SpinnakerException ex)
                            {
                                Console.WriteLine("Error: {0}", ex.Message);
                            }
                        }
                        Console.WriteLine("miss:{0} in 1000", miss);
                        stopwatch2.Stop();
                        Console.WriteLine("time:{0}", stopwatch2.ElapsedMilliseconds / 100.0);
                        stopwatch2.Reset();
                        break;
                    case AcquireMode.MULTIFRAME:
                        for (int imageCnt = 0; imageCnt < NumImages; imageCnt++)
                        {
                            try
                            {
                                using (IManagedImage rawImage = cam.GetNextImage())
                                {
                                    if (rawImage.IsIncomplete)
                                    {
                                        Console.WriteLine("图像不完整，状态为： {0}...", rawImage.ImageStatus);
                                    }
                                    else
                                    {
                                        uint width = rawImage.Width;

                                        uint height = rawImage.Height;

                                        Console.WriteLine("当前图像 {0}, width = {1}, height = {2}", imageCnt, width, height);

                                        using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.Mono8))
                                        {
                                            ImageBox.Source = ToBitmapSource(convertedImage.bitmap);
                                            String filename = SavePath + "/" + "Acquisition-CSharp-";
                                            if (deviceSerialNumber != "")
                                            {
                                                filename = filename + deviceSerialNumber + "-";
                                            }
                                            filename = filename + imageCnt + ".jpg";
                                            convertedImage.Save(filename);
                                            //Console.WriteLine("图像 {0} 已储存\n", filename);
                                        }
                                    }
                                }
                            }
                            catch (SpinnakerException ex)
                            {
                                Console.WriteLine("Error: {0}", ex.Message);
                            }
                        }
                        break;
                    case AcquireMode.SINGLEFRAME:
                        try
                        {
                            //stopwatch2.Start();
                            using (IManagedImage rawImage = cam.GetNextImage())
                            {
                                if (rawImage.IsIncomplete)
                                {
                                    Console.WriteLine("图像不完整，状态为： {0}...", rawImage.ImageStatus);
                                }
                                else
                                {
                                    int width = (int)rawImage.Width;
                                    int height = (int)rawImage.Height;
                                    Console.WriteLine("当前图像 {0}, width = {1}, height = {2}", 0, width, height);

                                    
                                    //Matrix<double> cameraMat = new Matrix<double>(3, 3);
                                    //cameraMat.Data[0, 0] = 2578.42;  cameraMat.Data[0, 1] = 0;         cameraMat.Data[0, 2] = 1266.60;
                                    //cameraMat.Data[1, 0] = 0;        cameraMat.Data[1, 1] = 2491.45;   cameraMat.Data[1, 2] = 1111.35;
                                    //cameraMat.Data[2, 0] = 0;        cameraMat.Data[2, 1] = 0;         cameraMat.Data[2, 2] = 1;
                                    //Matrix<double> distortionMat = new Matrix<double>(1, 4);
                                    //distortionMat.Data[0, 0] = -0.078470991609759;
                                    //distortionMat.Data[0, 1] = 0.109106631527471;
                                    //distortionMat.Data[0, 2] = 0;
                                    //distortionMat.Data[0, 3] = 0;
                                    //Matrix<double> mapxMat = new Matrix<double>(height, width);
                                    //Matrix<double> mapyMat = new Matrix<double>(height, width);
                                    //CvInvoke.InitUndistortRectifyMap(cameraMat, distortionMat, null, cameraMat, new System.Drawing.Size((int)width, (int)height), DepthType.Cv8U, mapxMat,mapyMat);
                                    
                                    using (IManagedImage convertedImage = rawImage.Convert(PixelFormatEnums.Mono8))
                                    {
                                        //ManagedImage currentImage = new ManagedImage(convertedImage);
                                        //畸变矫正
                                        //CvInvoke.Remap(convertedImage, currentImage, mapxMat, mapyMat, Inter.Linear);


                                        //显示
                                        ImageBox.Source = ToBitmapSource(convertedImage.bitmap);
                                        //检测高亮点
                                        IntPtr imgPtr = convertedImage.DataPtr;
                                        byte maxPiexl;
                                        double X_ind, Y_ind;

                                        List<double[,]> list = new List<double[,]>();
                                        for (int y = 0; y < height; y++)
                                        {
                                            //mono8格式，忽略边缘两个像素
                                            maxPiexl = Marshal.ReadByte(imgPtr, (int)(y * width+2));
                                            X_ind = 2;
                                            Y_ind = y;



                                            for (int x = 3; x < width - 2; x++)
                                            {
                                                byte currentPiexl = Marshal.ReadByte(imgPtr, (int)(y * width + x));
                                                if (currentPiexl > maxPiexl)
                                                {
                                                    maxPiexl = currentPiexl;
                                                    X_ind = x;
                                                    Y_ind = y;
                                                }

                                            }



                                            //重心法求光心，取邻域2个像素距离
                                            byte piexel0 = Marshal.ReadByte(imgPtr, (int)(Y_ind * width+X_ind-2));
                                            byte piexel1= Marshal.ReadByte(imgPtr, (int)(Y_ind * width+X_ind-1));
                                            byte piexel2= Marshal.ReadByte(imgPtr, (int)(Y_ind * width+X_ind+1));
                                            byte piexel3= Marshal.ReadByte(imgPtr, (int)(Y_ind * width+X_ind+2));

                                            X_ind = (piexel0 * (X_ind - 2) + piexel1 * (X_ind - 1) + maxPiexl * X_ind + piexel2 * (X_ind + 1) + piexel3 * (X_ind + 2)) / (piexel0 + piexel1 + maxPiexl + piexel2 + piexel3);
                                            //Console.WriteLine("(x,y)=>({0},{1})", X_ind, Y_ind);

                                            //距离计算
                                            double halfHeightPic = 1111.35;
                                            double halfWidthPic = 1266.60;
                                            //全部转换为mm
                                            double perPiexl = 3.45 / 1000.0;//单位像素大小
                                            double f = 8.596;//8.596;//焦距
                                            double s = 250;//基线长度
                                            double beta0 = 68*Math.PI/180;//初始度数
                                            //double epsilon = 0 * Math.PI / 180;
                                            //double offset0 = -halfWidthPic* perPiexl;//待拟合
                                            double beta1;

                                            double _d, _f, d, offset;
                                            
                                            _f = f / Math.Cos(Math.Atan(Math.Abs(Y_ind - halfHeightPic) * perPiexl / f));
                                            //offset = offset0 + f * (Math.Tan(epsilon + Math.PI/2 - beta0) - Math.Tan(Math.PI/2 - beta0));
                                            offset = f / Math.Tan(beta0);
                                            //beta1 = Math.Atan(_f / (halfWidthPic*perPiexl + offset));
                                            beta1 = Math.Atan(_f / offset);
                                            _d = s * _f / ((X_ind - halfWidthPic) * perPiexl + offset);
                                            d = f * s / ((X_ind - halfWidthPic) * perPiexl + offset);

                                            double xx, yy, zz;
                                            xx = _d * Math.Tan(Math.PI/2 - beta1);
                                            yy = _d * Math.Sin(Math.Atan(Math.Abs(Y_ind - halfHeightPic) * perPiexl / f));
                                            zz = d;

                                            double[,] xyz = new double[1,3];
                                            xyz[0, 0] = beta0 > Math.PI/2 ? xx : -xx;
                                            xyz[0, 1] = Y_ind > halfHeightPic ? yy : -yy;
                                            xyz[0, 2] = zz;
                                            list.Add(xyz);
                                            Console.WriteLine("[{3}\t,{4}\t](x,y,z)=>({0}\t,{1}\t,{2}\t)", Math.Round(xyz[0,0],3), Math.Round(xyz[0,1], 3), Math.Round(xyz[0,2], 3),Y_ind, Math.Round(X_ind, 3));//math.round(x,3)保留3为小数
                                        }
                                        //保存list
                                        Util.PLYUtil ply = new Util.PLYUtil(".");
                                        ply.PLYWriter(list,"raw.ply");
                                    }
                                }
                            }
                            
                            Console.WriteLine("获取单张显示计算耗时：{0}", stopwatch2.ElapsedMilliseconds);
                            
                        }
                        catch (SpinnakerException ex)
                        {
                            Console.WriteLine("Error: {0}", ex.Message);
                        }
                        break;
                }

                //
                // 结束捕获图像
                //
                // *** NOTES ***
                // 保持设备内存干净，而不用重启设备
                //
                cam.EndAcquisition();

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        private void Multiply_Bt(object sender, RoutedEventArgs e)
        {
            NumImages = int.Parse(GetNum.Text);

            //
            // Run example on each camera
            //
            // *** NOTES ***
            // 可以从 IManagedCamera 中获取相机对象，或者使用索引器
            // 使用using或Dispose()释放相机 C#中若直接释放单例会自动释放相机对象
            //
            try
            {
                RunSingleCamera(AcquireMode.MULTIFRAME);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        private void Continue_Bt(object sender, RoutedEventArgs e)
        {           
            time.Start();

            try
            {
                RunSingleCamera(AcquireMode.CONTINUOUS);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
            time.Stop();
        }

        private void Single_Bt(object sender, RoutedEventArgs e)
        {
            stopwatch1.Start();
            
            try
            {
                RunSingleCamera(AcquireMode.SINGLEFRAME);
            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
            
            stopwatch1.Stop();
            Console.WriteLine("按钮触发运行时间：{0}", stopwatch1.ElapsedMilliseconds);
            stopwatch1.Reset();
        }

        private void SetSavaPath(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog openFileDialog = new System.Windows.Forms.FolderBrowserDialog();  //选择文件夹
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SavePath = openFileDialog.SelectedPath;
            }
            // 确保文件的写权限
            FileStream fileStream;
            try
            {
                fileStream = new FileStream(SavePath + "test.txt", FileMode.Create);
                fileStream.Close();
                File.Delete("test.txt");
            }
            catch
            {
                Console.WriteLine("权限不足");
                return;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (cam != null)
            {
                cam.DeInit();
                cam.Dispose();
            }
            if (camList.Count!=0)
            {
                camList.Clear();
            }            
            system.Dispose();
            Console.WriteLine("\nDone!");
        }

        private void Setting_Bt(object sender, RoutedEventArgs e)
        {
            GUIFactory AcquisitionGUI = new GUIFactory();

            //AcquisitionGUI.ConnectGUILibrary(cam);

            CameraSelectionWindow camSelection = AcquisitionGUI.GetCameraSelectionWindow();

            camSelection.ShowModal(true);

        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
        //耗时12毫秒（可能存在内存泄漏）
        public static ImageSource ToBitmapSource(Bitmap p_bitmap)
        {
            IntPtr hBitmap = p_bitmap.GetHbitmap();
            ImageSource wpfBitmap;
            try
            {
                wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                //p_bitmap.Dispose();
                DeleteObject(hBitmap);
            }
            return wpfBitmap;
        }


    }
}

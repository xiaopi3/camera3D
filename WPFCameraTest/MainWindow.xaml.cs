using System;
using System.Collections.Generic;
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

using SpinnakerNET.GUI;
using SpinnakerNET;

namespace WPFCameraTest
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ManagedSystem ms = new ManagedSystem();

            IList< IManagedCamera> camList = ms.GetCameras();

            IManagedCamera cam = camList[0];

            GUIFactory AcquisitionGUI = new GUIFactory();

            cam.Init();
            //AcquisitionGUI.ConnectGUILibrary(cam);

            ImageDrawingWindow AcquisitionDrawing = AcquisitionGUI.GetImageDrawingWindow();

            //AcquisitionDrawing.Connect(cam);

            //AcquisitionDrawing.Start();
            //AcquisitionDrawing.Stop();

            AcquisitionDrawing.ShowModal();


        }
    }
}

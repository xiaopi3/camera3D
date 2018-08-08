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

namespace camera3D.UserControls
{
    /// <summary>
    /// SliderWithEnable.xaml 的交互逻辑
    /// </summary>
    public partial class SliderWithEnable : UserControl
    {
        public delegate void setSliderEventDelegate();
        public setSliderEventDelegate setSliderEvent;

        public delegate void setCheckedEventDelegate();
        public setSliderEventDelegate setCheckedEvent;

        public delegate void setUnCheckEventDelegate();
        public setSliderEventDelegate setUnCheckEvent;

        public SliderWithEnable()
        {
            InitializeComponent();

        }

        private void sliderCheck_Checked(object sender, RoutedEventArgs e)
        {
            slider.IsEnabled = true;
            setCheckedEvent?.Invoke();
        }

        private void sliderCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            slider.IsEnabled = false;
            setUnCheckEvent?.Invoke();
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            setSliderEvent?.Invoke();
        }
    }
}

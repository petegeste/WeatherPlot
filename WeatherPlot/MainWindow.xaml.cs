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

namespace WeatherPlot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModel();
            WeatherChart.AxisY.NoisyCollectionChanged += AxisY_NoisyCollectionChanged;

        }

        private void AxisY_NoisyCollectionChanged(IEnumerable<LiveCharts.Wpf.Axis> oldItems, IEnumerable<LiveCharts.Wpf.Axis> newItems)
        {
            foreach (var axis in newItems)
            {
                axis.Title = "Max Temp (F)";
                axis.LabelFormatter = (value) => $"{Math.Round(value, 1)} °F";
            }
        }
    }
}

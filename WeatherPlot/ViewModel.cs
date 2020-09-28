using GalaSoft.MvvmLight.Command;
using LiveCharts;
using LiveCharts.Wpf;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WeatherPlot
{
    /// <summary>
    /// Application logic for WeatherPlot.
    /// </summary>
    public class ViewModel : INotifyPropertyChanged
    {
        //Event raised when property is changed
        public event PropertyChangedEventHandler PropertyChanged;

        private Weather weather = new Weather(Properties.Resources.WeatherAPIKey);

        #region properties

        private string _cityLocation = "Nowhere, USA";
        /// <summary>
        /// Location of the city after weather results are loaded
        /// </summary>
        public string CityLocation
        {
            get => _cityLocation;
            set
            {
                if (_cityLocation != value)
                {
                    _cityLocation = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _searchText;
        /// <summary>
        /// Town to search for
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ICommand _searchCommand;
        /// <summary>
        /// Command initiates search on OpenWeatherMaps
        /// </summary>
        public ICommand SearchCommand
        {
            get => _searchCommand;
            set
            {
                if (_searchCommand != value)
                {
                    _searchCommand = value;
                    RaisePropertyChanged();
                }
            }
        }

        private SeriesCollection _temperatures;
        /// <summary>
        /// A collection of the temperature values over the next several days
        /// </summary>
        public SeriesCollection Temperatures
        {
            get => _temperatures;
            set
            {
                if (_temperatures != value)
                {
                    _temperatures = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string[] _axisLabels;
        /// <summary>
        /// Day labels for chart
        /// </summary>
        public string[] AxisLabels
        {
            get => _axisLabels;
            set
            {
                if (_axisLabels != value)
                {
                    _axisLabels = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion
        // Construct the view model
        public ViewModel()
        {
            SearchCommand = new RelayCommand(PerformSearch);
        }

        /// <summary>
        /// Performs a search using the OpenWeatherMaps API
        /// </summary>
        private async void PerformSearch()
        {
            string city, state;
            if (!weather.CheckInputText(SearchText, out city, out state))
            {
                MessageBox.Show("Please check your input. Only certain characters are accepted!");
                return;
            }

            var res = await weather.GetWeatherAsync(SearchText);

            // check results
            if (!res.Successful)
            {
                MessageBox.Show(res.Message);
                return;
            }

            CityLocation = weather.GetLocationName();

            var data = weather.GetTemperatureSeries().ToList();
            AxisLabels = data.Select(d => d.day).ToArray();

            SeriesCollection series = new SeriesCollection()
            {
                new LineSeries()
                {
                    Title = "Max Temperatures",
                    Values = new ChartValues<double>(data.Select(d => d.max_temp ?? 0)),
                    Stroke = Brushes.DarkRed,
                    Fill = Brushes.Transparent
                },
                new LineSeries()
                {
                    Title = "Min Temperatures",
                    Values = new ChartValues<double>(data.Select(d => d.min_temp ?? 0)),
                    Stroke = Brushes.CornflowerBlue,
                    Fill = Brushes.Transparent
                }
            };

            Temperatures = series;
        }


        /// <summary>
        /// Raises the property changed event
        /// </summary>
        /// <param name="property">Optionally, the given name of the property</param>
        private void RaisePropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WeatherPlot
{

    /// <summary>
    ///  Represents the root of the XML document
    /// </summary>
    [Mappable("weatherdata")]
    public class WeatherXmlModel
    {

        public static WeatherXmlModel Parse(XElement xml)
        {
            if (xml == null)
            {
                return null;
            }
            WeatherXmlModel model = new WeatherXmlModel();
            try
            {
                if (!Composition.TryDeserialize(xml, model))
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }

            return model;
        }

        [ChildMapping(typeof(LocationXmlModel))]
        public List<LocationXmlModel> Location = new List<LocationXmlModel>();
        [ListMapping("forecast", typeof(ForecastXmlModel))]
        public List<ForecastXmlModel> ForecastDays = new List<ForecastXmlModel>();
    }

    [Mappable("location")]
    public class LocationXmlModel
    {
        [ValueChildMapping("name")]
        public string City;
        [ValueChildMapping("country")]
        public string Country;
    }

    [Mappable("time")]
    public class ForecastXmlModel
    {
        [Mapping("day")]
        public string Day;

        [ChildMapping(typeof(TemperatureXmlModel))]
        public List<TemperatureXmlModel> Temperature = new List<TemperatureXmlModel>();
    }

    [Mappable("temperature")]
    public class TemperatureXmlModel
    {
        [Mapping("day")]
        public double Day;
        [Mapping("night")]
        public double Night;
        [Mapping("min")]
        public double Min;
        [Mapping("max")]
        public double Max;

        public double KelvinToFarenheit(double kelvin)
        {
            return ((kelvin - 273.15) * (9.0 / 5.0)) + 32.0;
        }

    }

}

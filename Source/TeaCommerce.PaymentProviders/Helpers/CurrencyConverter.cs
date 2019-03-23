using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TeaCommerce.PaymentProviders.Classic.ING_Models;
//using System.Web;

namespace TeaCommerce.PaymentProviders.Helpers
{
    public class CurrencyConverter
    {

        public static async Task<CurrencyConverterModel> ToEuroAsync(string currency, decimal amount)
        {
            var converted = new CurrencyConverterModel
            {
                OriginalCurrency = currency,
                OriginalAmount = amount
            };

            if (string.IsNullOrEmpty(currency))
                throw new ArgumentNullException("Please provide a valid currency value");

            var client = new HttpClient
            {
                BaseAddress = new Uri("https://api.exchangeratesapi.io/latest")
            };

            var result = await client.GetAsync($"?base={currency}");
            if(result.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"No valid response received from {client.BaseAddress.ToString()} statuscode: {result.StatusCode}");
            }
            var content = await result.Content.ReadAsStringAsync();
            var jsonObject = JObject.Parse(content);
            converted.RateDate = DateTime.Parse(jsonObject["date"].ToString());
            var rates = jsonObject["rates"];

            var currentRate = (double)rates["EUR"];
            converted.ConversionRate = currentRate;

            var convertedAmount = amount * (decimal)currentRate;
            converted.ConvertedAmount = decimal.Round(convertedAmount, 2, MidpointRounding.AwayFromZero);

            return converted;           
        }
    }
}

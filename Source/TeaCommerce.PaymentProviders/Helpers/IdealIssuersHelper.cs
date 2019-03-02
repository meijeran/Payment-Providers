using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace TeaCommerce.PaymentProviders.Helpers
{
    public class IdealIssuersHelper
    { 
        private static string url = "";
        private static string key = "";

        public static List<Idealissuer> GetIdealissuers()
        {
            IEnumerable<Idealissuer> items = Enumerable.Empty<Idealissuer>();

            using (var client = CreateClient(url,key))
            {
                var result = client.GetAsync("ideal/issuers/").GetAwaiter().GetResult();
                var issuers = result?.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                items = JsonConvert.DeserializeObject<IEnumerable<Idealissuer>>(issuers);
            }

            return items.ToList();
        }

        private static HttpClient CreateClient(string apiUrl, string apiKey)
        {
            if (string.IsNullOrEmpty(apiUrl))
                throw new Exception("Please provide an apiUrl");

            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("Please provide an apiKey");

            var client = new HttpClient
            {
                BaseAddress = new Uri(apiUrl)
            };

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00);

            var plain = Encoding.UTF8.GetBytes($"{apiKey}:");
            var encoded = Convert.ToBase64String(plain);

            var authValue = new AuthenticationHeaderValue("Basic", encoded);
            client.DefaultRequestHeaders.Authorization = authValue;
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",encoded);

            return client;
        }
    }

    public class Idealissuer
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}

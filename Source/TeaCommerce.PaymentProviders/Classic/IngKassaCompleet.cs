using System.Collections.Generic;
using System.Web;
using TeaCommerce.Api.Common;
using TeaCommerce.Api.Models;
using TeaCommerce.Api.Web.PaymentProviders;
using System.Net.Http;
using System;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using TeaCommerce.Api.Services;
using System.Net;
using TeaCommerce.Api.Infrastructure.Logging;

namespace TeaCommerce.PaymentProviders.Classic
{

    [PaymentProvider("ING Kassa Compleet CreditCard")]
    public class KassaCompleet : APaymentProvider
    {
        private string apiUrl => DefaultSettings["apiUrl"];
        private IDictionary<string, string> CurrentSetting;
        private CreditcardResult CreditcardResult;
        private string continueUrl;
        private string callbackUrl;
        public KassaCompleet()
        {
            CreditcardResult = null;
        }

        public override IDictionary<string, string> DefaultSettings
        {
            get
            {
                var defaultSettings = new Dictionary<string, string>();
                defaultSettings["apiKey"] = string.Empty;
                defaultSettings["apiUrl"] = string.Empty;
                defaultSettings["returnUrl"] = string.Empty;
                defaultSettings["cancelUrl"] = string.Empty;

                return defaultSettings;
            }

        }

        public override PaymentHtmlForm GenerateHtmlForm(Order order, string teaCommerceContinueUrl, string teaCommerceCancelUrl, string teaCommerceCallBackUrl, string teaCommerceCommunicationUrl, IDictionary<string, string> settings)
        {
            order.MustNotBeNull(nameof(order));
            settings.MustNotBeNull("settings");
            
            var key = settings["apiKey"];
            var url = settings["apiUrl"];
            CurrentSetting = settings;
            continueUrl = teaCommerceContinueUrl;
            callbackUrl = teaCommerceCallBackUrl;


            var form = new PaymentHtmlForm();            
            if (CreditcardResult == null)
            {
                var result = CreditCardPaymentAsync(order, url, key).Result;
                CreditcardResult = result;
            }

            if (CreditcardResult == null)
            {
                LoggingService.Instance.Log("No valid result from ING kassa compleet");
                return null;
            }
            if (!CreditcardResult.transactions.Any()) {
                LoggingService.Instance.Log("No valid response from ING kassa compleet");
                return null;
            }

            var transaction = CreditcardResult.transactions.FirstOrDefault();
            var fields = new KeyValuePair<string, string>(nameof(transaction.payment_method), transaction.payment_method);

            form.InputFields["continueurl"] = teaCommerceContinueUrl;
            form.InputFields["cancelurl"] = teaCommerceCancelUrl;
            form.InputFields["callbackurl"] = teaCommerceCallBackUrl;

           
            
            //form.Attributes.Add(fields);

            form.InputFields.Add(fields);


            order.TransactionInformation.TransactionId = CreditcardResult.id;
            
            
            form.Action = transaction.payment_url;            
            form.Method = HtmlFormMethodAttribute.Get;
            order.Save();
            return form;            
        }

        public override string GetCancelUrl(Order order, IDictionary<string, string> settings)
        {
            return string.Empty;
        }

        public override bool FinalizeAtContinueUrl => true;

        public override string GetContinueUrl(Order order, IDictionary<string, string> settings)
        {
            return settings["returnUrl"];           
        }

        public override CallbackInfo ProcessCallback(Order order, HttpRequest request, IDictionary<string, string> settings)
        {
            var price = order.TotalPrice.WithVat;
            var finalize = this.FinalizeAtContinueUrl;
            
            //todo: check if payment was succesfull
            
            return new CallbackInfo(price, order.OrderNumber, PaymentState.Captured);

            //else
            //throw exception

            
            //throw new System.NotImplementedException();
        }

        public override ApiInfo CapturePayment(Order order, IDictionary<string, string> settings)
        {
            return base.CapturePayment(order, settings);
        }

        private async Task<CreditcardResult> CreditCardPaymentAsync(Order order, string url, string key)
        {
            var client = CreateClient(url, key);
            PaymentMethod paymentMethod = PaymentMethodService.Instance.Get(order.StoreId, order.PaymentInformation.PaymentMethodId.Value);
            var payment = new CreditCardPayment
            {
                OrderId = "1",
                Description = string.Join(",", order.OrderLines.Select(ol => ol.Name)),
                TotalAmount = (int)order.TotalPrice.WithVat * 100,
                ReturnUrl = continueUrl
            };

            var serialized = await JsonConvert.SerializeObjectAsync(payment);
            var payload = new StringContent(serialized, Encoding.UTF8, "application/json");

            try
            {
                var result = await client.PostAsync("/v1/orders/", payload);
                var response = await result.Content.ReadAsStringAsync();

                if(result.StatusCode == HttpStatusCode.BadRequest)
                {
                    LoggingService.Instance.Log(string.Format("Bad request: {0}", result.Content.ReadAsStringAsync()));
                    return null;
                }

                var creditcardResult = await JsonConvert.DeserializeObjectAsync<CreditcardResult>(response);

                if (creditcardResult != null)
                {
                    if (!string.IsNullOrEmpty(creditcardResult.transactions.FirstOrDefault().payment_url))
                    {
                        return creditcardResult;
                    }
                }
            }
            catch(Exception ex)
            {
                LoggingService.Instance.Log(ex);
            }
            finally
            {
                client.Dispose();
            }

            return null;
        }

        private HttpClient CreateClient(string apiUrl, string apiKey)
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

            var plain = Encoding.UTF8.GetBytes($"{apiKey}:{string.Empty}");
            var encoded = Convert.ToBase64String(plain);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",encoded);

            return client;
        }
    }
}
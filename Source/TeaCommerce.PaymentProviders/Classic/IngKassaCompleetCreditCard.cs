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
using TeaCommerce.PaymentProviders.Helpers;

namespace TeaCommerce.PaymentProviders.Classic
{

    [PaymentProvider("ING Kassa Compleet CreditCard")]
    public class IngKassaCompleetCreditCard : APaymentProvider
    {
        private IDictionary<string, string> CurrentSetting;
        private PaymentResult PaymentResult;
        private string continueUrl;
        private string callbackUrl;
        public IngKassaCompleetCreditCard()
        {
            PaymentResult = null;
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
            if (PaymentResult == null)
            {
                var result = PostCreditCardPayment(order, url, key).Result;
                PaymentResult = result;
            }

            if (PaymentResult == null)
            {
                LoggingService.Instance.Log("No valid result from ING kassa compleet");
                return null;
            }
            if (!PaymentResult.transactions.Any()) {
                LoggingService.Instance.Log("No valid response from ING kassa compleet");
                return null;
            }

            var transaction = PaymentResult.transactions.FirstOrDefault();
            var fields = new KeyValuePair<string, string>(nameof(transaction.payment_method), transaction.payment_method);

            form.InputFields["continueurl"] = teaCommerceContinueUrl;
            form.InputFields["cancelurl"] = teaCommerceCancelUrl;
            form.InputFields["callbackurl"] = teaCommerceCallBackUrl;

            form.InputFields.Add(fields);
            order.TransactionInformation.TransactionId = PaymentResult.id;           
            
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

        public IDictionary<string, string> CurrentSetting1 { get => CurrentSetting; set => CurrentSetting = value; }

        public override string GetContinueUrl(Order order, IDictionary<string, string> settings)
        {
            return settings["returnUrl"];           
        }

        public override CallbackInfo ProcessCallback(Order order, HttpRequest request, IDictionary<string, string> settings)
        {
            order.MustNotBeNull(nameof(order));
            var orderId = request.QueryString["order_id"];
            var key = settings["apiKey"]; 
            var url = settings["apiUrl"];

            var paymentResult = GetPaymentInfo(orderId, url: url, key: key).GetAwaiter().GetResult();
            var transactions = paymentResult?.transactions;
            var callbackInfo = new CallbackInfo(order.PaymentInformation.TotalPrice.WithVat, paymentResult?.id, PaymentState.Error);
            if (transactions != null && transactions.Any())
            {
                var transaction = transactions.FirstOrDefault();
                if(transaction.status == "pending")
                {
                    callbackInfo.PaymentState = PaymentState.PendingExternalSystem;
                }
                if(transaction.status == "completed")
                {                    
                    order.Finalize(order.TotalPrice.WithVat, paymentResult.id,PaymentState.Captured);
                    callbackInfo.PaymentState = PaymentState.Captured;
                }                
            }

            return callbackInfo;
        }

        public override ApiInfo CapturePayment(Order order, IDictionary<string, string> settings)
        {
            return base.CapturePayment(order, settings);
        }

        private async Task<PaymentResult> PostCreditCardPayment(Order order, string url, string key)
        {
            var client = CreateClient(url, key);
            PaymentMethod paymentMethod = PaymentMethodService.Instance.Get(order.StoreId, order.PaymentInformation.PaymentMethodId.Value);
            
            var currency = Api.Web.TeaCommerceHelper.GetCurrency(order.StoreId, order.CurrencyId);
            var price =  order.TotalPrice.WithVat;

            if(currency.IsoCode != "EUR")
            {
                var converted = await CurrencyConverter.ToEuroAsync(currency.IsoCode, price);
                price = converted.ConvertedAmount;
            }

            var p = price * 100;
            int totalAmount = Convert.ToInt32(p);

            var payment = new CreditCardPayment
            {
                OrderId = order.CartNumber,
                Description = string.Join(",", order.OrderLines.Select(ol => ol.Name)),
                TotalAmount = totalAmount,
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

                var creditcardResult = await JsonConvert.DeserializeObjectAsync<PaymentResult>(response);

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

            var plain = Encoding.UTF8.GetBytes($"{apiKey}:");
            var encoded = Convert.ToBase64String(plain);

            var authValue = new AuthenticationHeaderValue("Basic", encoded);
            client.DefaultRequestHeaders.Authorization = authValue;            

            return client;
        }

        private async Task<PaymentResult> GetPaymentInfo(string orderId, string url, string key)
        {
            var client = CreateClient(url, key);
            var uri = new Uri($"{url}/v1/orders/{orderId}/");
            
            var responseMessage = await client.GetAsync(uri);

            if(responseMessage.StatusCode == HttpStatusCode.OK)
            {
                var content = await responseMessage.Content.ReadAsStringAsync();
                var result = await JsonConvert.DeserializeObjectAsync<PaymentResult>(content);

                return result;
            }

            LoggingService.Instance.Log($"Error retrieving payment info. HttpStatusCode: {responseMessage.StatusCode.ToString()}");
            return null;
        }
    }
}
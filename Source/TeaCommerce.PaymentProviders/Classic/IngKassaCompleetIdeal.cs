using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TeaCommerce.Api.Common;
using TeaCommerce.Api.Infrastructure.Logging;
using TeaCommerce.Api.Models;
using TeaCommerce.Api.Services;
using TeaCommerce.Api.Web.PaymentProviders;
using TeaCommerce.PaymentProviders.Helpers;

namespace TeaCommerce.PaymentProviders.Classic
{
    [PaymentProvider("ING Kassa Compleet Ideal")]
    public class IngKassaCompleetIdeal : APaymentProvider
    {
        private string continueUrl;
        private PaymentResult PaymentResult;
        private string callbackUrl;

        public override IDictionary<string, string> DefaultSettings
        {
            get
            {
                var defaultSettings = new Dictionary<string, string>
                {
                    ["apiKey"] = string.Empty,
                    ["apiUrl"] = string.Empty,
                    ["returnUrl"] = string.Empty,
                    ["cancelUrl"] = string.Empty
                };

                return defaultSettings;
            }
        }

         
        public override PaymentHtmlForm GenerateHtmlForm(Order order, string teaCommerceContinueUrl, string teaCommerceCancelUrl, string teaCommerceCallBackUrl, string teaCommerceCommunicationUrl, IDictionary<string, string> settings)
        {
            order.MustNotBeNull(nameof(order));
            settings.MustNotBeNull("settings");
            
            var key = settings["apiKey"];
            var url = settings["apiUrl"];
            //CurrentSetting = settings;
            continueUrl = teaCommerceContinueUrl;
            CallbackUrl = teaCommerceCallBackUrl;

            var issuer = order.Properties.FirstOrDefault(x => x.Alias.ToLower().Equals("issuers"));

            if(issuer == null)
            {
                LoggingService.Instance.Log("No valid ideal issuer");
                throw new Exception("No valid ideal Issuer");
            }
            var form = new PaymentHtmlForm();
            

            if (PaymentResult == null)
            {
                var result = InitializePayment(order, url, key, issuer.Value).Result;
                PaymentResult = result;
            }

            if (PaymentResult == null)
            {
                LoggingService.Instance.Log("No valid result from ING kassa compleet");
                return null;
            }
            if (!PaymentResult.transactions.Any())
            {
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
      
        private async Task<PaymentResult> InitializePayment(Order order, string url, string key, string issuer)
        {
            var client = CreateClient(url, key);
            PaymentMethod paymentMethod = PaymentMethodService.Instance.Get(order.StoreId, order.PaymentInformation.PaymentMethodId.Value);

            var currency = Api.Web.TeaCommerceHelper.GetCurrency(order.StoreId, order.CurrencyId);
            var price = order.TotalPrice.WithVat;

            if (currency.IsoCode != "EUR")
            {
                var converted = await CurrencyConverter.ToEuroAsync(currency.IsoCode, price);
                price = converted.ConvertedAmount;
            }

            var p = price * 100;
            int totalAmount = Convert.ToInt32(p);

            var payment = new IdealPayment
            {
                OrderId = order.Id.ToString(),
                Description = string.Join(",", order.OrderLines.Select(ol => ol.Name) ),
                TotalAmount = totalAmount,
                ReturnUrl = continueUrl
            };

            payment.AddIssuer(issuer);

            var serialized = await JsonConvert.SerializeObjectAsync(payment);
            var payload = new StringContent(serialized, Encoding.UTF8, "application/json");

            try
            {
                var result = await client.PostAsync("/v1/orders/", payload);
                var response = await result.Content.ReadAsStringAsync();

                if (result.StatusCode == HttpStatusCode.BadRequest)
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
            catch (Exception ex)
            {
                LoggingService.Instance.Log(ex);
            }
            finally
            {
                client.Dispose();
            }

            return null;
        }

        public override bool FinalizeAtContinueUrl => true;

        public string CallbackUrl { get => callbackUrl; set => callbackUrl = value; }

        private HttpClient CreateClient(string url, string key)
        {
            if (string.IsNullOrEmpty(url))
                throw new Exception("Please provide an apiUrl");

            if (string.IsNullOrEmpty(key))
                throw new Exception("Please provide an apiKey");

            var client = new HttpClient
            {
                BaseAddress = new Uri(url)
            };

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(0xc0 | 0x300 | 0xc00);

            var plain = Encoding.UTF8.GetBytes($"{key}:");
            var encoded = Convert.ToBase64String(plain);

            var authValue = new AuthenticationHeaderValue("Basic", encoded);
            client.DefaultRequestHeaders.Authorization = authValue;
          
            return client;
        }

        public override string GetCancelUrl(Order order, IDictionary<string, string> settings)
        {
            return string.Empty;
        }

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
                if (transaction.status == "pending")
                {
                    callbackInfo.PaymentState = PaymentState.PendingExternalSystem;
                }
                if (transaction.status == "completed")
                {
                    order.Finalize(order.TotalPrice.WithVat, paymentResult.id, PaymentState.Captured);
                    callbackInfo.PaymentState = PaymentState.Captured;
                }
            }

            
            return callbackInfo;
        }

        private async Task<PaymentResult> GetPaymentInfo(string orderId, string url, string key)
        {
            var client = CreateClient(url, key);
            var uri = new Uri($"{url}/v1/orders/{orderId}/");
            
            var responseMessage = await client.GetAsync(uri);

            if (responseMessage.StatusCode == HttpStatusCode.OK)
            {
                var content = await responseMessage.Content.ReadAsStringAsync();
                var result = await JsonConvert.DeserializeObjectAsync<PaymentResult>(content);

                return result;
            }

            LoggingService.Instance.Log($"Error retrieving payment info. HttpStatusCode: {responseMessage.StatusCode.ToString()}");
            return null;
        }

        public override ApiInfo CancelPayment(Order order, IDictionary<string, string> settings)
        {
            return base.CancelPayment(order, settings);
        }
    }
}

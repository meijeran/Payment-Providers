using System;

namespace TeaCommerce.PaymentProviders.Classic
{
    internal class EventResult
    {
        public string @event { get; set; }
        public string id { get; set; }
        public DateTime noticed { get; set; }
        public DateTime occurred { get; set; }
        public string source { get; set; }
    }
}
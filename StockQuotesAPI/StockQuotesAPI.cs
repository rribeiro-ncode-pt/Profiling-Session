using HandlerInterfaces;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Timers;

namespace StockQuotesAPI
{
    public class StockQuotesAPI : IConfigurableHandler
    {
        private string defaultResponseType = "application/json";
        private static List<Stock> stocks = new List<Stock>
            {
                new Stock("AAPL", 150.00, 1000000),
                new Stock("MSFT", 250.00, 800000),
                new Stock("GOOGL", 2800.00, 300000),
                new Stock("AMZN", 3300.00, 400000),
                new Stock("TSLA", 700.00, 1200000),
                new Stock("FB", 330.00, 900000),
                new Stock("NVDA", 600.00, 700000),
                new Stock("PYPL", 280.00, 500000),
                new Stock("ADBE", 550.00, 300000),
                new Stock("NFLX", 530.00, 400000),
                new Stock("CMCSA", 55.00, 1500000),
                new Stock("CSCO", 52.00, 1800000),
                new Stock("PEP", 145.00, 600000),
                new Stock("AVGO", 470.00, 250000),
                new Stock("TXN", 185.00, 400000),
                new Stock("TMUS", 130.00, 500000),
                new Stock("QCOM", 140.00, 700000),
                new Stock("INTC", 55.00, 2000000),
                new Stock("SBUX", 110.00, 800000),
                new Stock("AMD", 85.00, 1300000)
            };

        public void Configure(Dictionary<string, JsonElement> settings)
        {
            if (settings.TryGetValue("DefaultResponseType", out var responseTypeElement))
            {
                defaultResponseType = responseTypeElement.GetString() ?? defaultResponseType;
            }

            System.Timers.Timer timer = new System.Timers.Timer(500);
            timer.Elapsed += (sender, e) =>
            {
                UpdateRandomStocks(stocks);
            };
            timer.Start();
        }

        static void UpdateRandomStocks(List<Stock> stocks)
        {
            Random rand = new Random();
            int numStocksToUpdate = rand.Next(1, 6);

            var stocksToUpdate = stocks.OrderBy(x => rand.Next()).Take(numStocksToUpdate).ToList();

            foreach (var stock in stocksToUpdate)
            {
                double variation = (rand.NextDouble() * 2 - 1) * 0.001;
                double newPrice = stock.Price * (1 + variation);
                stock.Variation = (newPrice - stock.Price) / stock.Price * 100;
                stock.Price = newPrice;

                int volumeIncrease = rand.Next(100, 1000000);
                stock.Volume += volumeIncrease;

                stock.LastUpdated = DateTime.Now;
            }
        }

        public async Task HandleRequestAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            string content = JsonSerializer.Serialize(stocks);
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentType = defaultResponseType;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }
    }

    class Stock
    {
        public string Symbol { get; set; }
        public double Price { get; set; }
        public double Variation { get; set; }
        public long Volume { get; set; }
        public DateTime LastUpdated { get; set; }

        public Stock(string symbol, double price, long volume)
        {
            Symbol = symbol;
            Price = price;
            Volume = volume;
            Variation = 0;
            LastUpdated = DateTime.Now;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        var stocks = new List<Stock>
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

        string htmlContent = GenerateHtml();
        File.WriteAllText("stock_data.html", htmlContent);
        Console.WriteLine("HTML file generated.");

        while (true)
        {
            UpdateRandomStocks(stocks);
            string jsonContent = JsonSerializer.Serialize(stocks);
            File.WriteAllText("stock_data.json", jsonContent);
            Console.WriteLine("JSON data updated.");
            Thread.Sleep(500);
        }
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

    static string GenerateHtml()
    {
        return @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <link href=""https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css"" rel=""stylesheet"">
    <title>NASDAQ Stock Quotes</title>
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js""></script>
    <script>
        function updateStockData() {
            $.getJSON('stock_data.json', function(data) {
                data.forEach(function(stock) {
                    let row = $(`#${stock.Symbol}`);
                    if (row.length === 0) {
                        // If row doesn't exist, create it
                        let newRow = `<tr id=""${stock.Symbol}"">
                            <td>${stock.Symbol}</td>
                            <td class=""price"">${stock.Price.toFixed(2)} USD</td>
                            <td class=""variation""></td>
                            <td class=""volume"">${stock.Volume.toLocaleString()}</td>
                        </tr>`;
                        $('#stockTableBody').append(newRow);
                        row = $(`#${stock.Symbol}`);
                    }

                    // Update only if the stock has been recently updated
                    if (new Date(stock.LastUpdated) > new Date(Date.now() - 2000)) {
                        let variationColor = stock.Variation >= 0 ? 'text-success' : 'text-danger';
                        let variationArrow = stock.Variation >= 0 ? '▲' : '▼';
                        row.find('.price').text(`${stock.Price.toFixed(2)} USD`).addClass('bg-warning');
                        row.find('.variation').html(`${variationArrow} ${Math.abs(stock.Variation).toFixed(2)}%`)
                            .removeClass('text-success text-danger').addClass(variationColor);
                        row.find('.volume').text(stock.Volume.toLocaleString()).addClass('bg-warning');
                        
                        // Remove highlighting after a short delay
                        setTimeout(() => {
                            row.find('.price, .volume').removeClass('bg-warning');
                        }, 1000);
                    }
                });
            });
        }

        $(document).ready(function() {
            updateStockData();
            setInterval(updateStockData, 1000);
        });
    </script>
    <style>
        .bg-warning { transition: background-color 1s; }
    </style>
</head>
<body>
    <div class=""container mt-5"">
        <h1 class=""text-center"">NASDAQ Stock Quotes</h1>
        <table class=""table table-striped mt-4"">
            <thead class=""thead-dark"">
                <tr>
                    <th>Symbol</th>
                    <th>Price</th>
                    <th>Variation</th>
                    <th>Volume</th>
                </tr>
            </thead>
            <tbody id=""stockTableBody"">
            </tbody>
        </table>
    </div>
</body>
</html>";
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        // Definir cotações iniciais
        var stocks = new List<Stock>
        {
            new Stock("AAPL", 150.00),
            new Stock("MSFT", 250.00),
            new Stock("GOOGL", 2800.00),
            new Stock("AMZN", 3300.00),
            new Stock("TSLA", 700.00)
        };

        // Criar página HTML a cada minuto
        while (true)
        {
            // Atualizar cotações com pequenas variações
            UpdateStockPrices(stocks);

            // Gerar e guardar o HTML
            string htmlContent = GenerateHtml(stocks);
            File.WriteAllText("cotacoes.html", htmlContent);

            Console.WriteLine("Página de cotações atualizada.");

            // Aguardar 1 minuto
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }

    static void UpdateStockPrices(List<Stock> stocks)
    {
        Random rand = new Random();
        foreach (var stock in stocks)
        {
            // Variação aleatória entre -1% e +1%
            double variation = (rand.NextDouble() * 2 - 1) * 0.01;
            stock.Price += stock.Price * variation;
        }
    }

    static string GenerateHtml(List<Stock> stocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<link href=\"https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css\" rel=\"stylesheet\">");
        sb.AppendLine("<title>Cotações da NASDAQ</title>");
        sb.AppendLine("<script>");
        sb.AppendLine("setTimeout(function() { location.reload(); }, 1000);"); // Adiciona auto-refresh a cada 1 segundo
        sb.AppendLine("</script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container mt-5\">");
        sb.AppendLine("<h1 class=\"text-center\">Cotações da NASDAQ</h1>");
        sb.AppendLine("<table class=\"table table-striped mt-4\">");
        sb.AppendLine("<thead class=\"thead-dark\">");
        sb.AppendLine("<tr><th>Símbolo</th><th>Preço</th></tr>");
        sb.AppendLine("</thead>");
        sb.AppendLine("<tbody>");
        foreach (var stock in stocks)
        {
            sb.AppendLine($"<tr><td>{stock.Symbol}</td><td>{stock.Price:F2} USD</td></tr>");
        }
        sb.AppendLine("</tbody>");
        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}

class Stock
{
    public string Symbol { get; set; }
    public double Price { get; set; }

    public Stock(string symbol, double price)
    {
        Symbol = symbol;
        Price = price;
    }
}

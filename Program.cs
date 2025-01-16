using System.Collections.Generic;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var deliveryCheckCancellation = new CancellationTokenSource();
_ = Task.Run(async () => await MonitorDeliveryStatusAsync(deliveryCheckCancellation.Token));


app.MapGet("/", async (HttpContext context) =>
{
  context.Response.WriteAsync("<h1> This is the Shopee Food Home Page</h1>");
});


app.MapGet("/orders", async (HttpContext context) =>
{
    context.Response.WriteAsync("<h1> This is Shopee Food Orders Page</h1>");
    try
    {
        var orders = await LoadOrdersFromFile();
        foreach (var order in orders)
        {
            await context.Response.WriteAsync($"Order ID: {order.OrderId}<br>");
            await context.Response.WriteAsync($"Restaurant ID: {order.RestaurantId}<br>");
            await context.Response.WriteAsync($"Food List: {order.FoodList}<br>");
            await context.Response.WriteAsync($"Address: {order.Address}<br></br>");
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});

app.MapPost("/orders", async (HttpRequest request) =>
{
    try
    {
        // Manually deserialize the request body
        var order = await JsonSerializer.DeserializeAsync<Order>(request.Body);
        if (order == null) return Results.BadRequest("Invalid order data");

        var orders = await LoadOrdersFromFile();
        orders.Add(order);
        await SaveOrdersToFile(orders);
        return Results.Ok(new { message = $"Order saved successfully: ID: {order.OrderId}, FoodList: {order.FoodList}, Address: {order.Address}," +
            $"Restaurant ID: {order.RestaurantId}"});
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});


app.MapGet("/restaurants/update", async (HttpContext context) =>
{
    context.Response.WriteAsync("<h1> Shopee Food Restaurants have been updated</h1>");
    try
    {
        var restaurants = await ProcessRestaurantsParallelAsync();
        foreach (var res in restaurants)
        {
            await context.Response.WriteAsync($"<div style='margin-bottom: 20px'>");
            await context.Response.WriteAsync($"Restaurant ID: {res.Id}<br>");
            await context.Response.WriteAsync($"Name: {res.Name}<br>");
            await context.Response.WriteAsync($"Status: {res.Status}<br>");
            await context.Response.WriteAsync($"Menu Items:<br>");
            foreach (var item in res.Menu)
            {
                await context.Response.WriteAsync($"- {item.Name}: ${item.Price}<br>");
            }
            await context.Response.WriteAsync($"</div>");
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});

app.MapGet("/restaurants", async (HttpContext context) =>
{
    context.Response.WriteAsync("<h1> This is the Shopee Food Restaurant Page</h1>");
    try
    {
        var restaurants = await ProcessRestaurantsParallelAsync();
        foreach (var res in restaurants)
        {
            await context.Response.WriteAsync($"<div style='margin-bottom: 20px'>");
            await context.Response.WriteAsync($"Restaurant ID: {res.Id}<br>");
            await context.Response.WriteAsync($"Name: {res.Name}<br>");
            await context.Response.WriteAsync($"Status: {res.Status}<br>");
            await context.Response.WriteAsync($"Menu Items:<br>");
            foreach (var item in res.Menu)
            {
                await context.Response.WriteAsync($"- {item.Name}: ${item.Price}<br>");
            }
            await context.Response.WriteAsync($"</div>");
        }
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(ex.Message);
    }
});


app.MapGet("/deliveries", (HttpContext context) =>
{
    return Results.Ok(DeliveryStatus.Current);
});


static async Task<List<Order>> LoadOrdersFromFile()
{
    const string fileName = "orders.json";
    if (!File.Exists(fileName))
    {
        return new List<Order>();
    }
    var json = await File.ReadAllTextAsync(fileName);
    return JsonSerializer.Deserialize<List<Order>>(json) ?? new List<Order>();
}

static async Task<List<Restaurant>> LoadRestaurantsFromFile()
{
    const string fileName = "restaurants.json";
    if (!File.Exists(fileName))
    {
        return new List<Restaurant>();
    }
    var json = await File.ReadAllTextAsync(fileName);
    return JsonSerializer.Deserialize<List<Restaurant>>(json) ?? new List<Restaurant>();
}

static async Task SaveOrdersToFile(List<Order> orders)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    var json = JsonSerializer.Serialize(orders, options);
    await File.WriteAllTextAsync("orders.json", json);
}

static async Task SaveRestaurantsToFile(List<Order> orders)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    var json = JsonSerializer.Serialize(orders, options);
    await File.WriteAllTextAsync("orders.json", json);
}


static async Task<IEnumerable<Restaurant>> ProcessRestaurantsParallelAsync()
{
    var restaurants = await LoadRestaurantsFromFile();

    var processedRestaurants = restaurants.AsParallel()
        .WithDegreeOfParallelism(10) // Limit concurrent operations
        .Select(async restaurant =>
        {
            restaurant.Menu = await UpdateMenuItemsAsync(restaurant.Id);
            restaurant.Status = await CheckOperationalStatusAsync(restaurant.Id);
            return restaurant;
        })
        .ToArray();

    // Wait for all parallel operations to complete
    return await Task.WhenAll(processedRestaurants);
}
static async Task<List<MenuItem>> UpdateMenuItemsAsync(string restaurantId)
{
    await Task.Delay(100); 
    return new List<MenuItem>
    {
        new MenuItem { Name = $"Dish 1 - {restaurantId}", Price = Random.Shared.Next(10, 50) },
        new MenuItem { Name = $"Dish 2 - {restaurantId}", Price = Random.Shared.Next(10, 50) }
    };
}
//static IEnumerable<Restaurant> GenerateSampleRestaurants()
//{
//    return Enumerable.Range(1, 50).Select(i => new Restaurant
//    {
//        Id = $"R{i:D3}",
//        Name = $"Restaurant {i}",
//        Address = $"Address {i}"
//    });
//}

static async Task MonitorDeliveryStatusAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var orders = await LoadOrdersFromFile();
            foreach (var order in orders)
            {
                var status = await CheckDeliveryStatusAsync(order.OrderId);
                DeliveryStatus.Current[order.OrderId] = status;
            }

            await Task.Delay(5000, cancellationToken); // Wait 5 seconds
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking delivery status: {ex.Message}");
            await Task.Delay(5000, cancellationToken);
        }
    }
}

static async Task<string> CheckOperationalStatusAsync(string restaurantId)
{
    await Task.Delay(50); // Simulate API call
    return Random.Shared.Next(2) == 0 ? "OPEN" : "CLOSED";
}
static async Task<string> CheckDeliveryStatusAsync(int orderId)
{
    await Task.Delay(100); // Simulate API call
    var statuses = new[] { "PREPARING", "ON_THE_WAY", "DELIVERED" };
    return statuses[Random.Shared.Next(statuses.Length)];
}

app.Run();
public class Order
{
    public int OrderId { get; set; }
    public string FoodList { get; set; }
    public string Address { get; set; }
    public string RestaurantId { get; set; }
    public string DeliveryStatus { get; set; } = "PENDING";
}
public class Restaurant
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public List<MenuItem> Menu { get; set; }
    public string Status { get; set; }
}

public class MenuItem
{
    public string Name { get; set; }
    public decimal Price { get; set; }
}
public static class DeliveryStatus
{
    public static Dictionary<int, string> Current { get; } = new();
}













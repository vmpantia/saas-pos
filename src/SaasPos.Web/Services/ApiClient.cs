using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using SaasPos.Application.Dtos;
using SaasPos.Domain.Enums;

namespace SaasPos.Web.Services;

public class ApiException(string message) : Exception(message);

/// <summary>Per-circuit sign-in state, persisted in the browser's session storage.</summary>
public class Session(ProtectedSessionStorage storage)
{
    private const string Key = "saaspos.session";
    public LoginResponse? Current { get; private set; }
    public bool IsSignedIn => Current is not null;
    public bool IsManager => Current is { Role: UserRole.Admin or UserRole.Manager };
    public event Action? Changed;

    public async Task RestoreAsync()
    {
        try
        {
            var r = await storage.GetAsync<LoginResponse>(Key);
            if (r.Success) Current = r.Value;
        }
        catch (Exception) { Current = null; }
        Changed?.Invoke();
    }

    public async Task SignInAsync(LoginResponse r)
    {
        Current = r;
        await storage.SetAsync(Key, r);
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Current = null;
        await storage.DeleteAsync(Key);
        Changed?.Invoke();
    }
}

public class ApiClient(HttpClient http, Session session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (session.Current is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Current.Token);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json);

        using var res = await http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.Unauthorized && session.IsSignedIn)
            await session.SignOutAsync();
        if (!res.IsSuccessStatusCode)
        {
            string? title = null;
            try { title = (await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("title").GetString(); } catch { }
            throw new ApiException(title ?? $"Request failed ({(int)res.StatusCode}).");
        }
        if (res.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public Task<LoginResponse> LoginAsync(LoginRequest r) => SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", r);
    public Task<List<ProductDto>> ProductsAsync(string? q = null, bool activeOnly = false) =>
        SendAsync<List<ProductDto>>(HttpMethod.Get, $"api/products?q={Uri.EscapeDataString(q ?? "")}&activeOnly={activeOnly}");
    public Task<ProductDto> CreateProductAsync(ProductRequest r) => SendAsync<ProductDto>(HttpMethod.Post, "api/products", r);
    public Task<ProductDto> UpdateProductAsync(Guid id, ProductRequest r) => SendAsync<ProductDto>(HttpMethod.Put, $"api/products/{id}", r);
    public Task<object> SetActiveAsync(Guid id, bool active) => SendAsync<object>(HttpMethod.Put, $"api/products/{id}/active/{active}");
    public Task<List<CategoryDto>> CategoriesAsync() => SendAsync<List<CategoryDto>>(HttpMethod.Get, "api/categories");
    public Task<CategoryDto> CreateCategoryAsync(string name) => SendAsync<CategoryDto>(HttpMethod.Post, "api/categories", new CategoryRequest(name));
    public Task<ProductDto> ReceiveAsync(Guid id, ReceiveStockRequest r) => SendAsync<ProductDto>(HttpMethod.Post, $"api/inventory/{id}/receive", r);
    public Task<ProductDto> AdjustAsync(Guid id, AdjustStockRequest r) => SendAsync<ProductDto>(HttpMethod.Post, $"api/inventory/{id}/adjust", r);
    public Task<List<StockMovementDto>> MovementsAsync(Guid id) => SendAsync<List<StockMovementDto>>(HttpMethod.Get, $"api/inventory/{id}/movements");
    public Task<SaleDto> CheckoutAsync(CheckoutRequest r) => SendAsync<SaleDto>(HttpMethod.Post, "api/sales/checkout", r);
    public Task<List<SaleDto>> SalesAsync(DateTime from, DateTime to) =>
        SendAsync<List<SaleDto>>(HttpMethod.Get, $"api/sales?from={from:O}&to={to:O}");
    public Task<SaleDto> VoidAsync(Guid id) => SendAsync<SaleDto>(HttpMethod.Post, $"api/sales/{id}/void");
    public Task<SalesSummaryDto> SummaryAsync(DateTime from, DateTime to) =>
        SendAsync<SalesSummaryDto>(HttpMethod.Get, $"api/reports/summary?from={from:O}&to={to:O}");
}

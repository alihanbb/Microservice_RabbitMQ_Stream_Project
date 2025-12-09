namespace ShoppingCartService.Infrastructure.Repositories;

public class RedisCartRepository : ICartRepository
{
    private readonly RedisConnectionFactory _connectionFactory;
    private const string CartKeyPrefix = "cart:user:";

    public RedisCartRepository(RedisConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(userId);

        var data = await db.StringGetAsync(key);

        if (data.IsNullOrEmpty)
            return null;

        return DeserializeCart(data!);
    }

    public async Task<Cart?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var db = _connectionFactory.GetDatabase();
        var server = _connectionFactory.GetServer();

        await foreach (var key in server.KeysAsync(pattern: $"{CartKeyPrefix}*"))
        {
            var data = await db.StringGetAsync(key);
            if (!data.IsNullOrEmpty)
            {
                var cart = DeserializeCart(data!);
                if (cart?.Id == cartId)
                    return cart;
            }
        }

        return null;
    }

    public async Task SaveAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(cart.UserId);

        var json = SerializeCart(cart);

        await db.StringSetAsync(key, json, TimeSpan.FromDays(7));
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(userId);

        await db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var db = _connectionFactory.GetDatabase();
        var key = GetCartKey(userId);

        return await db.KeyExistsAsync(key);
    }

    private static string GetCartKey(Guid userId) => $"{CartKeyPrefix}{userId}";

    private static string SerializeCart(Cart cart)
    {
        var cartData = new CartRedisModel
        {
            Id = cart.Id,
            UserId = cart.UserId,
            CreatedAt = cart.CreatedAt,
            UpdatedAt = cart.UpdatedAt,
            IsConfirmed = cart.IsConfirmed,
            Items = cart.Items.Select(i => new CartItemRedisModel
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Category = i.Category,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList()
        };

        return JsonSerializer.Serialize(cartData);
    }

    private static Cart? DeserializeCart(string json)
    {
        var cartData = JsonSerializer.Deserialize<CartRedisModel>(json);

        if (cartData == null)
            return null;

        var cart = Cart.Create(cartData.UserId);

        var idField = typeof(Cart).GetProperty("Id");
        var createdAtField = typeof(Cart).GetProperty("CreatedAt");
        var updatedAtField = typeof(Cart).GetProperty("UpdatedAt");
        var isConfirmedField = typeof(Cart).GetProperty("IsConfirmed");

        idField?.SetValue(cart, cartData.Id);
        createdAtField?.SetValue(cart, cartData.CreatedAt);
        updatedAtField?.SetValue(cart, cartData.UpdatedAt);
        isConfirmedField?.SetValue(cart, cartData.IsConfirmed);

        foreach (var itemData in cartData.Items)
        {
            var item = CartItem.Create(
                itemData.ProductId,
                itemData.ProductName,
                itemData.Category,
                itemData.Quantity,
                itemData.Price
            );
            cart.AddItem(item);
        }

        return cart;
    }

    private class CartRedisModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsConfirmed { get; set; }
        public List<CartItemRedisModel> Items { get; set; } = new();
    }

    private class CartItemRedisModel
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}

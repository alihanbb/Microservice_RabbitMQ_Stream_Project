using ShoppingCartService.Application.Interfaces;
using ShoppingCartService.Domain.Entities;

namespace ShoppingCartService.Infrastructure.Seed;

public class CartSeedData
{
    private readonly ICartRepository _cartRepository;

    public CartSeedData(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedTasks = GetSampleUsers().Select(async user =>
        {
            var existingCart = await _cartRepository.GetByUserIdAsync(user.UserId, cancellationToken);
            if (existingCart != null)
                return;

            var cart = Cart.Create(user.UserId);
            foreach (var item in user.Items)
            {
                cart.AddItem(item);
            }

            await _cartRepository.SaveAsync(cart, cancellationToken);
        });

        await Task.WhenAll(seedTasks);
    }

    private static List<SampleUserCart> GetSampleUsers()
    {
        return
        [
            new SampleUserCart(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                [
                    CreateItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Wireless Mouse Logitech MX Master 3", "Electronics", 1, 99.99m),
                    CreateItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaab", "Mechanical Keyboard RGB", "Electronics", 1, 149.99m),
                    CreateItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaac", "USB-C Hub 7-in-1", "Electronics", 2, 45.00m),
                    CreateItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaad", "Monitor Stand Adjustable", "Electronics", 1, 79.99m),
                    CreateItem("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaae", "Webcam 4K Ultra HD", "Electronics", 1, 129.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                [
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1", "Notebook A5 Premium", "Office", 10, 4.99m),
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2", "Ballpoint Pen Set (12 pack)", "Office", 3, 8.50m),
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3", "Desk Organizer Wood", "Office", 1, 35.00m),
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4", "Sticky Notes Colorful (500 sheets)", "Office", 5, 6.99m),
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5", "Paper Clips Box (1000 pcs)", "Office", 2, 3.49m),
                    CreateItem("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb6", "Whiteboard Markers Set", "Office", 4, 12.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                [
                    CreateItem("cccccccc-cccc-cccc-cccc-ccccccccccc1", "Gaming Headset 7.1 Surround", "Gaming", 1, 89.99m),
                    CreateItem("cccccccc-cccc-cccc-cccc-ccccccccccc2", "Gaming Mouse Pad XXL", "Gaming", 1, 24.99m),
                    CreateItem("cccccccc-cccc-cccc-cccc-ccccccccccc3", "Controller Stand RGB", "Gaming", 2, 19.99m),
                    CreateItem("cccccccc-cccc-cccc-cccc-ccccccccccc4", "Gaming Chair Cushion", "Gaming", 1, 45.00m),
                    CreateItem("cccccccc-cccc-cccc-cccc-ccccccccccc5", "Cable Management Kit", "Gaming", 1, 15.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                [
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd1", "Clean Code - Robert C. Martin", "Books", 1, 42.99m),
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd2", "Design Patterns - Gang of Four", "Books", 1, 54.99m),
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd3", "Domain-Driven Design - Eric Evans", "Books", 1, 59.99m),
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd4", "Book Light LED Rechargeable", "Books", 1, 14.99m),
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd5", "Bookmarks Set (50 pcs)", "Books", 2, 9.99m),
                    CreateItem("dddddddd-dddd-dddd-dddd-ddddddddddd6", "Reading Glasses Blue Light", "Books", 1, 29.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                [
                    CreateItem("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1", "Ergonomic Office Chair", "Furniture", 1, 299.99m),
                    CreateItem("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee2", "Standing Desk Converter", "Furniture", 1, 189.99m),
                    CreateItem("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee3", "Desk Lamp LED Adjustable", "Furniture", 1, 49.99m),
                    CreateItem("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee4", "Footrest Ergonomic", "Furniture", 1, 39.99m),
                    CreateItem("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee5", "Cable Tray Under Desk", "Furniture", 2, 24.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                [
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff1", "Scientific Calculator", "Education", 1, 24.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff2", "Highlighter Set Neon (8 colors)", "Education", 3, 7.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff3", "Backpack Laptop 15.6 inch", "Education", 1, 59.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff4", "USB Flash Drive 128GB", "Education", 2, 18.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff5", "Index Cards 500 Pack", "Education", 4, 5.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff6", "Pencil Case Large", "Education", 1, 12.99m),
                    CreateItem("ffffffff-ffff-ffff-ffff-fffffffffff7", "Ruler Set Geometry", "Education", 1, 8.49m)
                ]),

            new SampleUserCart(
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                [
                    CreateItem("11111111-2222-3333-4444-555555555551", "Resistance Bands Set", "Fitness", 1, 29.99m),
                    CreateItem("11111111-2222-3333-4444-555555555552", "Yoga Mat Premium 6mm", "Fitness", 1, 39.99m),
                    CreateItem("11111111-2222-3333-4444-555555555553", "Water Bottle Stainless Steel 1L", "Fitness", 2, 22.99m),
                    CreateItem("11111111-2222-3333-4444-555555555554", "Fitness Tracker Band", "Fitness", 1, 79.99m),
                    CreateItem("11111111-2222-3333-4444-555555555555", "Foam Roller 18 inch", "Fitness", 1, 24.99m),
                    CreateItem("11111111-2222-3333-4444-555555555556", "Gym Towel Quick Dry (3 pack)", "Fitness", 1, 19.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("88888888-8888-8888-8888-888888888888"),
                [
                    CreateItem("22222222-3333-4444-5555-666666666661", "Sketchbook A4 200 pages", "Art", 3, 14.99m),
                    CreateItem("22222222-3333-4444-5555-666666666662", "Colored Pencils Set (72 colors)", "Art", 1, 34.99m),
                    CreateItem("22222222-3333-4444-5555-666666666663", "Acrylic Paint Set (24 colors)", "Art", 1, 29.99m),
                    CreateItem("22222222-3333-4444-5555-666666666664", "Paint Brush Set (15 pcs)", "Art", 2, 18.99m),
                    CreateItem("22222222-3333-4444-5555-666666666665", "Canvas Panels 8x10 (12 pack)", "Art", 1, 24.99m),
                    CreateItem("22222222-3333-4444-5555-666666666666", "Easel Tabletop Adjustable", "Art", 1, 45.00m)
                ]),

            new SampleUserCart(
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                [
                    CreateItem("33333333-4444-5555-6666-777777777771", "Coffee Grinder Electric", "Kitchen", 1, 49.99m),
                    CreateItem("33333333-4444-5555-6666-777777777772", "French Press 1L", "Kitchen", 1, 29.99m),
                    CreateItem("33333333-4444-5555-6666-777777777773", "Coffee Mug Set (4 pcs)", "Kitchen", 1, 24.99m),
                    CreateItem("33333333-4444-5555-6666-777777777774", "Coffee Beans Premium 1kg", "Kitchen", 2, 19.99m),
                    CreateItem("33333333-4444-5555-6666-777777777775", "Milk Frother Handheld", "Kitchen", 1, 14.99m),
                    CreateItem("33333333-4444-5555-6666-777777777776", "Coffee Scale Digital", "Kitchen", 1, 22.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                []),

            new SampleUserCart(
                Guid.Parse("12345678-1234-1234-1234-123456789012"),
                [
                    CreateItem("44444444-5555-6666-7777-888888888881", "MacBook Pro 14 inch M3", "Premium Electronics", 1, 1999.99m),
                    CreateItem("44444444-5555-6666-7777-888888888882", "Apple Watch Ultra 2", "Premium Electronics", 1, 799.99m),
                    CreateItem("44444444-5555-6666-7777-888888888883", "AirPods Pro 2nd Gen", "Premium Electronics", 1, 249.99m),
                    CreateItem("44444444-5555-6666-7777-888888888884", "iPad Pro 12.9 inch", "Premium Electronics", 1, 1099.99m),
                    CreateItem("44444444-5555-6666-7777-888888888885", "Apple Pencil 2nd Gen", "Premium Electronics", 1, 129.99m)
                ]),

            new SampleUserCart(
                Guid.Parse("abcdefab-cdef-abcd-efab-cdefabcdefab"),
                [
                    CreateItem("55555555-6666-7777-8888-999999999991", "A4 Paper Ream (500 sheets)", "Office Supplies", 50, 8.99m),
                    CreateItem("55555555-6666-7777-8888-999999999992", "Printer Ink Cartridge Black", "Office Supplies", 20, 24.99m),
                    CreateItem("55555555-6666-7777-8888-999999999993", "Printer Ink Cartridge Color", "Office Supplies", 15, 34.99m),
                    CreateItem("55555555-6666-7777-8888-999999999994", "File Folders (100 pack)", "Office Supplies", 10, 19.99m),
                    CreateItem("55555555-6666-7777-8888-999999999995", "Binder Clips Assorted", "Office Supplies", 25, 4.99m)
                ])
        ];
    }

    private static CartItem CreateItem(string productId, string name, string category, int quantity, decimal price)
        => CartItem.Create(Guid.Parse(productId), name, category, quantity, price);

    private sealed record SampleUserCart(Guid UserId, List<CartItem> Items);
}

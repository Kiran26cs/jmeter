namespace ProductApi.Models;

public record Product(int Id, string Name, decimal Price, int Stock);

public record CreateProductRequest(string Name, decimal Price, int Stock);

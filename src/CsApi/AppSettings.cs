namespace Api;


public record AuthSettings {
    public required string Issuer { get; init; }
    public required string Secret { get; init; }
}

public enum StoreType {
    Local = 0
}

public record StoreSettings {
    public required StoreType Type { get; init; }
    public required string Uri { get; init; }
}


public record AppSettings {
    public required AuthSettings Auth { get; init; }
    public required StoreSettings Store { get; init; }
}

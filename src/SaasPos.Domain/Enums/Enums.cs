namespace SaasPos.Domain.Enums;

public enum UserRole { Cashier = 0, Manager = 1, Admin = 2 }
public enum StockMovementType { Purchase = 0, Sale = 1, Adjustment = 2, Return = 3 }
public enum PaymentMethod { Cash = 0, Card = 1, EWallet = 2 }
public enum SaleStatus { Completed = 0, Voided = 1 }

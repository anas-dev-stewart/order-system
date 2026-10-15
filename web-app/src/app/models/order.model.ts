export interface Order {
  id: number;
  customerName: string;
  productId: number;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  createdAtUtc: string;
}

export interface OrderCreateRequest {
  customerName: string;
  productId: number;
  quantity: number;
}

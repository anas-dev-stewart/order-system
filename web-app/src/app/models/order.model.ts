export enum OrderStatus {
  Submitted = 0,
  Approved = 1,
  Rejected = 2
}

export interface Order {
  id: number;
  customerName: string;
  productId: number;
  quantity: number;
  unitPrice: number | null;
  totalPrice: number | null;
  status: OrderStatus;
  rejectionReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface OrderCreateRequest {
  customerName: string;
  productId: number;
  quantity: number;
}

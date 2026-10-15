export interface Product {
  id: number;
  name: string;
  price: number;
  stockQuantity: number;
}

export interface ProductCreateRequest {
  name: string;
  price: number;
  stockQuantity: number;
}

export type ProductUpdateRequest = ProductCreateRequest;

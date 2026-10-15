import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../api-config';
import { Order, OrderCreateRequest } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly baseUrl = `${API_CONFIG.orderServiceBaseUrl}/api/orders`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<Order[]> {
    return this.http.get<Order[]>(this.baseUrl);
  }

  create(request: OrderCreateRequest): Observable<Order> {
    return this.http.post<Order>(this.baseUrl, request);
  }
}

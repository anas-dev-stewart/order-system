import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subscription, interval } from 'rxjs';
import { OrderService } from '../../services/order.service';
import { ProductService } from '../../services/product.service';
import { Order, OrderStatus } from '../../models/order.model';
import { Product } from '../../models/product.model';

const POLL_INTERVAL_MS = 3000;

@Component({
  selector: 'app-orders-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './orders-page.component.html',
  styleUrl: './orders-page.component.css'
})
export class OrdersPageComponent implements OnInit, OnDestroy {
  orders: Order[] = [];
  products: Product[] = [];
  loading = false;
  errorMessage: string | null = null;

  selectedProductId: number | null = null;
  customerName = '';
  quantity: number | null = null;
  placingOrder = false;

  private pollSubscription: Subscription | null = null;

  constructor(private orderService: OrderService, private productService: ProductService) {}

  ngOnInit(): void {
    this.loadOrders();
    this.loadProducts();
    // Orders move from Submitted to Approved/Rejected asynchronously once
    // the worker finishes, so the table polls in the background rather
    // than requiring a manual refresh to see the status change.
    this.pollSubscription = interval(POLL_INTERVAL_MS).subscribe(() => this.refreshOrdersSilently());
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  loadOrders(): void {
    this.loading = true;
    this.orderService.getAll().subscribe({
      next: (orders) => {
        this.orders = [...orders].sort((a, b) => b.id - a.id);
        this.loading = false;
      },
      error: (err) => {
        this.errorMessage = this.extractError(err);
        this.loading = false;
      }
    });
  }

  private refreshOrdersSilently(): void {
    // Same as loadOrders, but doesn't toggle `loading` so the background
    // poll doesn't flash the "Loading orders..." state on every tick.
    this.orderService.getAll().subscribe({
      next: (orders) => (this.orders = [...orders].sort((a, b) => b.id - a.id)),
      error: () => {
        /* transient poll failures are silent; the next tick will retry */
      }
    });
  }

  loadProducts(): void {
    this.productService.getAll().subscribe({
      next: (products) => (this.products = products),
      error: () => {
        /* product dropdown just stays empty; order form will surface errors on submit */
      }
    });
  }

  productName(productId: number): string {
    return this.products.find((p) => p.id === productId)?.name ?? `#${productId}`;
  }

  statusLabel(status: OrderStatus): string {
    return OrderStatus[status] ?? `Unknown (${status})`;
  }

  placeOrder(): void {
    if (!this.customerName.trim() || this.selectedProductId === null || this.quantity === null) {
      this.errorMessage = 'Please choose a product and enter a customer name and quantity.';
      return;
    }

    this.errorMessage = null;
    this.placingOrder = true;
    this.orderService
      .create({
        customerName: this.customerName.trim(),
        productId: this.selectedProductId,
        quantity: this.quantity
      })
      .subscribe({
        next: () => {
          this.customerName = '';
          this.quantity = null;
          this.placingOrder = false;
          this.loadOrders();
          this.loadProducts();
        },
        error: (err) => {
          this.errorMessage = this.extractError(err);
          this.placingOrder = false;
        }
      });
  }

  private extractError(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && 'message' in err.error) {
      return (err.error as { message: string }).message;
    }
    if (err.status === 0) {
      return 'Could not reach the service. Is it running?';
    }
    return `Request failed with status ${err.status}.`;
  }
}

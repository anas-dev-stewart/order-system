import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ProductService } from '../../services/product.service';
import { Product } from '../../models/product.model';

type SortDirection = 'asc' | 'desc' | null;

@Component({
  selector: 'app-products-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './products-page.component.html',
  styleUrl: './products-page.component.css'
})
export class ProductsPageComponent implements OnInit {
  products: Product[] = [];
  loading = false;
  errorMessage: string | null = null;

  newName = '';
  newPrice: number | null = null;
  newStock: number | null = null;

  editingId: number | null = null;
  editName = '';
  editPrice: number | null = null;
  editStock: number | null = null;

  searchTerm = '';
  sortDirection: SortDirection = null;
  pageSize = 5;
  currentPage = 1;

  constructor(private productService: ProductService) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    this.errorMessage = null;
    this.productService.getAll().subscribe({
      next: (products) => {
        this.products = products;
        this.loading = false;
      },
      error: (err) => {
        this.errorMessage = this.extractError(err);
        this.loading = false;
      }
    });
  }

  get filteredAndSorted(): Product[] {
    let result = this.products;

    const term = this.searchTerm.trim().toLowerCase();
    if (term) {
      result = result.filter((p) => p.name.toLowerCase().includes(term));
    }

    if (this.sortDirection) {
      result = [...result].sort((a, b) =>
        this.sortDirection === 'asc' ? a.price - b.price : b.price - a.price
      );
    }

    return result;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredAndSorted.length / this.pageSize));
  }

  get pagedProducts(): Product[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredAndSorted.slice(start, start + this.pageSize);
  }

  onSearchChange(): void {
    this.currentPage = 1;
  }

  toggleSortByPrice(): void {
    if (this.sortDirection === null) {
      this.sortDirection = 'asc';
    } else if (this.sortDirection === 'asc') {
      this.sortDirection = 'desc';
    } else {
      this.sortDirection = null;
    }
    this.currentPage = 1;
  }

  goToPreviousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
    }
  }

  goToNextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
    }
  }

  addProduct(): void {
    if (!this.newName.trim() || this.newPrice === null || this.newStock === null) {
      this.errorMessage = 'Please fill in name, price and stock quantity.';
      return;
    }

    this.errorMessage = null;
    this.productService
      .create({ name: this.newName.trim(), price: this.newPrice, stockQuantity: this.newStock })
      .subscribe({
        next: () => {
          this.newName = '';
          this.newPrice = null;
          this.newStock = null;
          this.loadProducts();
        },
        error: (err) => {
          this.errorMessage = this.extractError(err);
        }
      });
  }

  startEdit(product: Product): void {
    this.editingId = product.id;
    this.editName = product.name;
    this.editPrice = product.price;
    this.editStock = product.stockQuantity;
  }

  cancelEdit(): void {
    this.editingId = null;
  }

  saveEdit(): void {
    if (this.editingId === null || !this.editName.trim() || this.editPrice === null || this.editStock === null) {
      return;
    }

    this.errorMessage = null;
    this.productService
      .update(this.editingId, { name: this.editName.trim(), price: this.editPrice, stockQuantity: this.editStock })
      .subscribe({
        next: () => {
          this.editingId = null;
          this.loadProducts();
        },
        error: (err) => {
          this.errorMessage = this.extractError(err);
        }
      });
  }

  deleteProduct(product: Product): void {
    if (!confirm(`Delete "${product.name}"?`)) {
      return;
    }

    this.errorMessage = null;
    this.productService.delete(product.id).subscribe({
      next: () => this.loadProducts(),
      error: (err) => {
        this.errorMessage = this.extractError(err);
      }
    });
  }

  private extractError(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && 'message' in err.error) {
      return (err.error as { message: string }).message;
    }
    if (err.status === 0) {
      return 'Could not reach the Product Service. Is it running?';
    }
    return `Request failed with status ${err.status}.`;
  }
}

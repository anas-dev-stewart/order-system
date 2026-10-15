import { Routes } from '@angular/router';
import { ProductsPageComponent } from './products/products-page/products-page.component';
import { OrdersPageComponent } from './orders/orders-page/orders-page.component';

export const routes: Routes = [
  { path: '', redirectTo: 'products', pathMatch: 'full' },
  { path: 'products', component: ProductsPageComponent },
  { path: 'orders', component: OrdersPageComponent }
];

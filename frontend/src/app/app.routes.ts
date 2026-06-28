import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ChatComponent } from './pages/chat/chat.component';

export const routes: Routes = [
  { path: '', component: ChatComponent, title: 'AI Chat' },
  { path: 'dashboard', component: DashboardComponent, title: 'Dashboard' },
  { path: '**', redirectTo: '' }
];

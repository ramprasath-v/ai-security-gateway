import { Routes } from '@angular/router';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ChatComponent } from './pages/chat/chat.component';
import { PolicySimulatorComponent } from './pages/policy-simulator/policy-simulator.component';
import { PoliciesComponent } from './pages/policies/policies.component';
import { ApplicationsComponent } from './pages/applications/applications.component';
import { AdminComponent } from './pages/admin/admin.component';

export const routes: Routes = [
  { path: '', component: ChatComponent, title: 'AI Chat' },
  { path: 'applications', component: ApplicationsComponent, title: 'Applications' },
  { path: 'policy-simulator', component: PolicySimulatorComponent, title: 'Policy Simulator' },
  { path: 'policies', component: PoliciesComponent, title: 'Policies' },
  { path: 'dashboard', component: DashboardComponent, title: 'Dashboard' },
  { path: 'admin', component: AdminComponent, title: 'Admin' },
  { path: '**', redirectTo: '' }
];

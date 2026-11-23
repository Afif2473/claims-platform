import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { ClaimDetailPage } from './pages/claim-detail';
import { ClaimListPage } from './pages/claim-list';
import { DashboardPage } from './pages/dashboard';
import { LoginPage } from './pages/login';
import { NewClaimPage } from './pages/new-claim';
import { NotificationsPage } from './pages/notifications';

export const routes: Routes = [
  { path: 'login', component: LoginPage, title: 'Sign in · Claims' },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'claims' },
      { path: 'claims', component: ClaimListPage, title: 'Claims' },
      { path: 'claims/new', component: NewClaimPage, title: 'Report an incident · Claims' },
      { path: 'claims/:id', component: ClaimDetailPage, title: 'Claim · Claims' },
      { path: 'dashboard', component: DashboardPage, title: 'Dashboard · Claims' },
      { path: 'notifications', component: NotificationsPage, title: 'Updates · Claims' },
    ],
  },
  { path: '**', redirectTo: '' },
];

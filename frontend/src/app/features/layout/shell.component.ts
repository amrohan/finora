import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { HouseholdService, Household } from '../../core/services/household.service';
import { PrivacyService } from '../../core/services/privacy.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    NzLayoutModule,
    NzMenuModule,
    NzDropDownModule,
    NzSelectModule,
    NzButtonModule,
    NzAvatarModule,
    NzToolTipModule
  ],
  template: `
    <nz-layout class="min-h-screen">
      <!-- Sidebar -->
      <nz-sider
        [nzCollapsible]="true"
        [(nzCollapsed)]="isCollapsed"
        [nzWidth]="250"
        class="bg-slate-900 border-r border-slate-800 shadow-sm">
        
        <!-- Brand Header -->
        <div class="h-16 flex items-center px-4 gap-3 bg-slate-950/40 border-b border-slate-800/80">
          <div class="w-9 h-9 rounded-lg bg-emerald-600 flex items-center justify-center text-white text-xl shadow-md flex-shrink-0">
            🪙
          </div>
          @if (!isCollapsed) {
            <div class="overflow-hidden">
              <span class="font-bold text-lg text-white tracking-tight block">Finora</span>
              <span class="text-[10px] text-emerald-400 font-medium tracking-wider uppercase block -mt-1">Family Wealth</span>
            </div>
          }
        </div>

        <!-- Navigation Menu -->
        <ul nz-menu nzMode="inline" nzTheme="dark" class="mt-3 bg-transparent border-0 space-y-1">
          <li nz-menu-item nzMatchRouter routerLink="/dashboard" class="rounded-lg mx-2">
            <span class="text-base mr-3">📊</span>
            <span>Dashboard</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/accounts" class="rounded-lg mx-2">
            <span class="text-base mr-3">🏦</span>
            <span>Accounts</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/transactions" class="rounded-lg mx-2">
            <span class="text-base mr-3">🧾</span>
            <span>Transactions</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/budgets" class="rounded-lg mx-2">
            <span class="text-base mr-3">🎯</span>
            <span>Budgets</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/bills" class="rounded-lg mx-2">
            <span class="text-base mr-3">📅</span>
            <span>Recurring Bills</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/debts" class="rounded-lg mx-2">
            <span class="text-base mr-3">📉</span>
            <span>Loans & Debt</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/goals" class="rounded-lg mx-2">
            <span class="text-base mr-3">🏆</span>
            <span>Savings Goals</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/analytics" class="rounded-lg mx-2">
            <span class="text-base mr-3">📈</span>
            <span>Analytics & Insights</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/settings" class="rounded-lg mx-2">
            <span class="text-base mr-3">⚙️</span>
            <span>Settings</span>
          </li>
        </ul>
      </nz-sider>

      <!-- Main Layout Body -->
      <nz-layout class="bg-slate-50">
        <!-- Top App Bar -->
        <nz-header class="h-16 bg-white border-b border-slate-200 px-6 flex items-center justify-between shadow-xs">
          <!-- Left: Active Household Selector -->
          <div class="flex items-center gap-3">
            <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Household:</span>
            <nz-select
              [ngModel]="selectedHouseholdId()"
              (ngModelChange)="onHouseholdChange($event)"
              class="w-52"
              nzSize="default"
              nzPlaceHolder="Select household">
              @for (h of households(); track h.id) {
                <nz-option [nzValue]="h.id" [nzLabel]="h.name + ' (' + h.role + ')'"></nz-option>
              }
            </nz-select>
          </div>

          <!-- Right: Actions & User Menu -->
          <div class="flex items-center gap-4">
            <!-- Privacy Shield Toggle -->
            <button
              nz-button
              nzType="text"
              nz-tooltip
              [nzTooltipTitle]="isPrivateMode() ? 'Reveal balances' : 'Hide balances (Public View)'"
              (click)="togglePrivacyMode()"
              class="flex items-center gap-2 text-slate-600 hover:text-slate-900 rounded-lg px-3 py-1.5 border border-slate-200 text-sm">
              <span>{{ isPrivateMode() ? '🙈' : '👁️' }}</span>
              <span class="text-xs font-medium">{{ isPrivateMode() ? 'Hidden' : 'Visible' }}</span>
            </button>

            <!-- User Profile Dropdown -->
            <a nz-dropdown [nzDropdownMenu]="userMenu" nzPlacement="bottomRight" class="flex items-center gap-2 cursor-pointer">
              <nz-avatar nzSize="small" [nzText]="userInitials()" class="bg-emerald-600 text-white font-medium"></nz-avatar>
              <span class="text-sm font-medium text-slate-800">{{ currentUser()?.fullName }}</span>
            </a>

            <nz-dropdown-menu #userMenu="nzDropdownMenu">
              <ul nz-menu class="min-w-44 shadow-lg rounded-xl border border-slate-100 p-1">
                <li nz-menu-item class="rounded-lg text-xs text-slate-500 py-1.5 px-3 pointer-events-none">
                  Signed in as <strong>{{ currentUser()?.email }}</strong>
                </li>
                <li nz-menu-divider></li>
                <li nz-menu-item routerLink="/settings" class="rounded-lg py-1.5 px-3">
                  Settings & Members
                </li>
                <li nz-menu-item (click)="logout()" class="rounded-lg text-rose-600 hover:bg-rose-50 py-1.5 px-3 font-medium">
                  Sign Out
                </li>
              </ul>
            </nz-dropdown-menu>
          </div>
        </nz-header>

        <!-- Page Content -->
        <nz-content class="p-6 overflow-y-auto">
          <router-outlet></router-outlet>
        </nz-content>
      </nz-layout>
    </nz-layout>
  `
})
export class ShellComponent implements OnInit {
  private authService = inject(AuthService);
  private householdService = inject(HouseholdService);
  private privacyService = inject(PrivacyService);
  private router = inject(Router);

  isCollapsed = false;
  isPrivateMode = this.privacyService.isMasked;
  currentUser = this.authService.currentUser;
  households = this.householdService.households;
  selectedHouseholdId = signal<string | null>(this.authService.activeHouseholdId());

  ngOnInit(): void {
    this.householdService.getUserHouseholds().subscribe(list => {
      if (list.length > 0 && !this.selectedHouseholdId()) {
        this.selectedHouseholdId.set(list[0].id);
        this.authService.setActiveHousehold(list[0].id);
      }
    });
  }

  userInitials(): string {
    const name = this.currentUser()?.fullName || 'User';
    return name.split(' ').map(p => p[0]).join('').toUpperCase().slice(0, 2);
  }

  togglePrivacyMode(): void {
    this.privacyService.toggle();
  }

  onHouseholdChange(id: string): void {
    this.selectedHouseholdId.set(id);
    this.authService.setActiveHousehold(id);
    window.location.reload();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}

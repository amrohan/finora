import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
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
    NzDropdownModule,
    NzSelectModule,
    NzButtonModule,
    NzAvatarModule,
    NzTooltipModule,
    NzIconModule,
    NzDrawerModule
  ],
  template: `
    <nz-layout class="min-h-screen">
      <!-- Desktop Sidebar (Hidden on mobile) -->
      <nz-sider
        [nzCollapsible]="true"
        [(nzCollapsed)]="isCollapsed"
        [nzWidth]="256"
        class="hidden md:block bg-slate-900 border-r border-slate-800 shadow-sm flex-shrink-0">
        
        <!-- Brand Header -->
        <div class="h-16 flex items-center px-4 gap-3 bg-slate-950/60 border-b border-slate-800">
          <div class="w-9 h-9 rounded-xl bg-gradient-to-tr from-emerald-600 to-teal-400 flex items-center justify-center text-white shadow-md flex-shrink-0">
            <span nz-icon nzType="wallet" class="text-lg"></span>
          </div>
          @if (!isCollapsed) {
            <div class="overflow-hidden">
              <span class="font-bold text-base text-white tracking-tight block">Finora</span>
              <span class="text-[10px] text-emerald-400 font-semibold tracking-wider uppercase block -mt-0.5">Family Wealth</span>
            </div>
          }
        </div>

        <!-- Navigation Menu -->
        <ul nz-menu nzMode="inline" nzTheme="dark" class="mt-3 bg-transparent border-0 space-y-1 px-1">
          <li nz-menu-item nzMatchRouter routerLink="/dashboard" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="dashboard" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Dashboard</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/accounts" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="bank" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Accounts</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/transactions" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="swap" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Transactions</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/budgets" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="pie-chart" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Budgets</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/bills" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="calendar" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Recurring Bills</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/debts" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="fall" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Loans & Debt</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/goals" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="trophy" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Savings Goals</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/analytics" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="rise" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Analytics & Insights</span>
          </li>
          <li nz-menu-item nzMatchRouter routerLink="/settings" class="rounded-xl mx-1 text-slate-300 hover:text-white">
            <span nz-icon nzType="setting" class="text-base mr-3"></span>
            <span class="font-medium text-sm">Settings</span>
          </li>
        </ul>
      </nz-sider>

      <!-- Main Layout Body -->
      <nz-layout class="bg-slate-50 min-h-screen flex flex-col">
        <!-- Top App Bar -->
        <nz-header class="h-16 bg-white border-b border-slate-200/80 px-4 sm:px-6 flex items-center justify-between sticky top-0 z-30 shadow-2xs">
          <!-- Left: Mobile Brand & Household Selector -->
          <div class="flex items-center gap-3">
            <!-- Mobile Brand Mark -->
            <div class="md:hidden flex items-center gap-2">
              <div class="w-8 h-8 rounded-lg bg-gradient-to-tr from-emerald-600 to-teal-400 flex items-center justify-center text-white shadow-xs">
                <span nz-icon nzType="wallet" class="text-base"></span>
              </div>
              <span class="font-bold text-slate-900 text-base">Finora</span>
            </div>

            <!-- Household Selector -->
            <div class="hidden sm:flex items-center gap-2">
              <span class="text-[11px] font-bold uppercase tracking-wider text-slate-400">Household:</span>
              <nz-select
                [ngModel]="selectedHouseholdId()"
                (ngModelChange)="onHouseholdChange($event)"
                class="w-48 sm:w-56"
                nzSize="small"
                nzPlaceHolder="Select household">
                @for (h of households(); track h.id) {
                  <nz-option [nzValue]="h.id" [nzLabel]="h.name + ' (' + h.role + ')'"></nz-option>
                }
              </nz-select>
            </div>
          </div>

          <!-- Right: Actions & User Menu -->
          <div class="flex items-center gap-2 sm:gap-3">
            <!-- Privacy Shield Toggle -->
            <button
              nz-button
              nzType="default"
              nzSize="small"
              nz-tooltip
              [nzTooltipTitle]="isPrivateMode() ? 'Reveal monetary balances' : 'Hide monetary balances (Privacy Mode)'"
              (click)="togglePrivacyMode()"
              class="flex items-center gap-1.5 rounded-lg border border-slate-200 text-slate-700 bg-slate-50 hover:bg-slate-100 font-medium px-2.5 py-1 text-xs">
              <span nz-icon [nzType]="isPrivateMode() ? 'eye-invisible' : 'eye'" class="text-sm"></span>
              <span class="hidden sm:inline">{{ isPrivateMode() ? 'Hidden' : 'Visible' }}</span>
            </button>

            <!-- User Profile Dropdown -->
            <a nz-dropdown [nzDropdownMenu]="userMenu" nzPlacement="bottomRight" class="flex items-center gap-2 cursor-pointer p-1 rounded-lg hover:bg-slate-100 transition-colors">
              <nz-avatar nzSize="small" [nzText]="userInitials()" class="bg-emerald-600 text-white font-semibold text-xs"></nz-avatar>
              <span class="hidden sm:inline text-sm font-medium text-slate-700">{{ currentUser()?.fullName }}</span>
            </a>

            <nz-dropdown-menu #userMenu="nzDropdownMenu">
              <ul nz-menu class="min-w-48 shadow-xl rounded-xl border border-slate-100 p-1">
                <li nz-menu-item class="rounded-lg text-xs text-slate-400 py-1.5 px-3 pointer-events-none">
                  Signed in as <strong class="text-slate-700 block truncate">{{ currentUser()?.email }}</strong>
                </li>
                <li nz-menu-divider></li>
                <li nz-menu-item routerLink="/settings" class="rounded-lg py-1.5 px-3 flex items-center gap-2 text-sm text-slate-700">
                  <span nz-icon nzType="setting"></span>
                  <span>Settings & Members</span>
                </li>
                <li nz-menu-item (click)="logout()" class="rounded-lg text-rose-600 hover:bg-rose-50 py-1.5 px-3 font-medium flex items-center gap-2 text-sm">
                  <span nz-icon nzType="logout"></span>
                  <span>Sign Out</span>
                </li>
              </ul>
            </nz-dropdown-menu>
          </div>
        </nz-header>

        <!-- Page Content Area -->
        <nz-content class="flex-1 p-4 sm:p-6 pb-24 md:pb-8 max-w-7xl w-full mx-auto overflow-y-auto">
          <router-outlet></router-outlet>
        </nz-content>

        <!-- Mobile Bottom App Navigation Bar (Only on mobile < md) -->
        <nav class="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white/95 backdrop-blur-md border-t border-slate-200/90 px-2 py-1.5 flex items-center justify-around shadow-lg">
          <a
            routerLink="/dashboard"
            routerLinkActive="text-emerald-600 font-semibold"
            [routerLinkActiveOptions]="{ exact: true }"
            class="flex flex-col items-center justify-center flex-1 py-1 text-slate-500 hover:text-emerald-600 transition-colors">
            <span nz-icon nzType="dashboard" class="text-xl"></span>
            <span class="text-[10px] mt-0.5">Home</span>
          </a>

          <a
            routerLink="/accounts"
            routerLinkActive="text-emerald-600 font-semibold"
            class="flex flex-col items-center justify-center flex-1 py-1 text-slate-500 hover:text-emerald-600 transition-colors">
            <span nz-icon nzType="bank" class="text-xl"></span>
            <span class="text-[10px] mt-0.5">Accounts</span>
          </a>

          <a
            routerLink="/transactions"
            routerLinkActive="text-emerald-600 font-semibold"
            class="flex flex-col items-center justify-center flex-1 py-1 text-slate-500 hover:text-emerald-600 transition-colors">
            <span nz-icon nzType="swap" class="text-xl"></span>
            <span class="text-[10px] mt-0.5">Activity</span>
          </a>

          <a
            routerLink="/analytics"
            routerLinkActive="text-emerald-600 font-semibold"
            class="flex flex-col items-center justify-center flex-1 py-1 text-slate-500 hover:text-emerald-600 transition-colors">
            <span nz-icon nzType="rise" class="text-xl"></span>
            <span class="text-[10px] mt-0.5">Analytics</span>
          </a>

          <button
            (click)="isMobileDrawerOpen.set(true)"
            class="flex flex-col items-center justify-center flex-1 py-1 text-slate-500 hover:text-emerald-600 transition-colors border-0 bg-transparent cursor-pointer">
            <span nz-icon nzType="appstore" class="text-xl"></span>
            <span class="text-[10px] mt-0.5">More</span>
          </button>
        </nav>

        <!-- Mobile More Menu Drawer -->
        <nz-drawer
          [nzVisible]="isMobileDrawerOpen()"
          [nzPlacement]="'bottom'"
          [nzHeight]="'auto'"
          [nzTitle]="drawerTitleTpl"
          (nzOnClose)="isMobileDrawerOpen.set(false)">
          <ng-container *nzDrawerContent>
            <div class="space-y-4 pb-6">
              <!-- Mobile Household Selector in Drawer -->
              <div class="sm:hidden bg-slate-50 p-3 rounded-xl border border-slate-100 space-y-1.5">
                <span class="text-[11px] font-bold uppercase tracking-wider text-slate-400">Current Household:</span>
                <nz-select
                  [ngModel]="selectedHouseholdId()"
                  (ngModelChange)="onHouseholdChange($event)"
                  class="w-full"
                  nzSize="default">
                  @for (h of households(); track h.id) {
                    <nz-option [nzValue]="h.id" [nzLabel]="h.name + ' (' + h.role + ')'"></nz-option>
                  }
                </nz-select>
              </div>

              <!-- More Nav Links Grid -->
              <div class="grid grid-cols-2 gap-2.5">
                <a
                  routerLink="/budgets"
                  (click)="isMobileDrawerOpen.set(false)"
                  class="flex items-center gap-3 p-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-slate-800 border border-slate-100 font-medium text-sm transition-colors">
                  <span nz-icon nzType="pie-chart" class="text-lg text-emerald-600"></span>
                  <span>Budgets</span>
                </a>

                <a
                  routerLink="/bills"
                  (click)="isMobileDrawerOpen.set(false)"
                  class="flex items-center gap-3 p-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-slate-800 border border-slate-100 font-medium text-sm transition-colors">
                  <span nz-icon nzType="calendar" class="text-lg text-blue-600"></span>
                  <span>Recurring Bills</span>
                </a>

                <a
                  routerLink="/debts"
                  (click)="isMobileDrawerOpen.set(false)"
                  class="flex items-center gap-3 p-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-slate-800 border border-slate-100 font-medium text-sm transition-colors">
                  <span nz-icon nzType="fall" class="text-lg text-rose-600"></span>
                  <span>Loans & Debt</span>
                </a>

                <a
                  routerLink="/goals"
                  (click)="isMobileDrawerOpen.set(false)"
                  class="flex items-center gap-3 p-3 rounded-xl bg-slate-50 hover:bg-slate-100 text-slate-800 border border-slate-100 font-medium text-sm transition-colors">
                  <span nz-icon nzType="trophy" class="text-lg text-amber-500"></span>
                  <span>Savings Goals</span>
                </a>
              </div>

              <div class="pt-2 border-t border-slate-100 flex items-center justify-between">
                <a
                  routerLink="/settings"
                  (click)="isMobileDrawerOpen.set(false)"
                  class="flex items-center gap-2 text-sm text-slate-600 font-medium py-2 px-3 rounded-lg hover:bg-slate-50">
                  <span nz-icon nzType="setting"></span>
                  <span>Settings</span>
                </a>

                <button
                  (click)="logout()"
                  class="flex items-center gap-2 text-sm text-rose-600 font-medium py-2 px-3 rounded-lg hover:bg-rose-50 border-0 bg-transparent cursor-pointer">
                  <span nz-icon nzType="logout"></span>
                  <span>Sign Out</span>
                </button>
              </div>
            </div>
          </ng-container>
        </nz-drawer>

        <ng-template #drawerTitleTpl>
          <div class="flex items-center gap-2">
            <span class="font-bold text-slate-900 text-base">Finora Menu</span>
          </div>
        </ng-template>
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
  isMobileDrawerOpen = signal(false);
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
    this.isMobileDrawerOpen.set(false);
    this.authService.logout();
    this.router.navigate(['/auth/login']);
  }
}

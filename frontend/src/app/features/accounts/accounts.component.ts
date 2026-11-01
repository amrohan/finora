import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { InrCurrencyPipe } from '../../shared/pipes/inr-currency.pipe';
import { AccountService, Account } from '../../core/services/account.service';

@Component({
  selector: 'app-accounts',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzInputNumberModule,
    NzTagModule,
    NzPopconfirmModule,
    NzEmptyModule,
    NzIconModule,
    InrCurrencyPipe
  ],
  template: `
    <div class="space-y-6 max-w-7xl mx-auto">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Accounts & Ledgers</h1>
          <p class="text-sm text-slate-500 mt-1">Bank accounts, credit cards, cash, and asset holdings</p>
        </div>
        <button
          nz-button
          nzType="primary"
          (click)="openCreateModal()"
          class="rounded-xl text-xs sm:text-sm font-medium bg-emerald-600 hover:bg-emerald-500 border-0 flex items-center gap-1.5 shadow-2xs">
          <span nz-icon nzType="plus"></span>
          <span>Add Account</span>
        </button>
      </div>

      <!-- Net Assets Summary Banner -->
      <div class="bg-gradient-to-r from-slate-900 to-slate-800 rounded-2xl p-6 text-white shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider block">Total Net Assets</span>
          <span class="text-3xl font-bold font-mono tracking-tight text-emerald-400 mt-1 block">
            {{ accountService.totalNetWorth() | inrCurrency }}
          </span>
        </div>
        <div class="flex items-center gap-6 text-sm text-slate-300">
          <div>
            <span class="text-slate-400 text-xs block">Active Accounts</span>
            <span class="font-bold text-lg text-white font-mono">{{ accountService.accounts().length }}</span>
          </div>
        </div>
      </div>

      <!-- Account Cards Grid -->
      @if (accountService.accounts().length === 0) {
        <nz-card class="rounded-xl border border-slate-200/80 text-center py-12">
          <nz-empty
            nzNotFoundImage="simple"
            nzNotFoundContent="No accounts added yet. Connect your bank accounts or cash holdings to begin.">
            <button
              nz-button
              nzType="primary"
              (click)="openCreateModal()"
              class="mt-3 bg-emerald-600 hover:bg-emerald-500 border-0 rounded-lg text-sm">
              Create First Account
            </button>
          </nz-empty>
        </nz-card>
      } @else {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          @for (acc of accountService.accounts(); track acc.id) {
            <nz-card class="rounded-xl border border-slate-200/80 shadow-xs hover:shadow-md transition-shadow">
              <div class="flex items-start justify-between gap-3 mb-3">
                <div class="flex items-center gap-3">
                  <div class="w-10 h-10 rounded-xl bg-slate-100 flex items-center justify-center text-slate-700 shadow-2xs">
                    <span nz-icon [nzType]="getAccountIcon(acc.type)" class="text-lg"></span>
                  </div>
                  <div>
                    <h3 class="font-bold text-slate-900 text-base leading-tight">{{ acc.name }}</h3>
                    <div class="flex items-center gap-1.5 mt-0.5">
                      <nz-tag [nzColor]="getTypeColor(acc.type)" class="text-[11px] font-medium rounded-md">
                        {{ getTypeName(acc.type) }}
                      </nz-tag>
                      @if (acc.accountNumberMask) {
                        <span class="text-xs text-slate-400 font-mono">{{ acc.accountNumberMask }}</span>
                      }
                    </div>
                  </div>
                </div>

                <!-- Visibility / Privacy Badge -->
                <div class="flex flex-col items-end gap-1">
                  @if (acc.visibility === 2 || acc.visibility === 'Private') {
                    <nz-tag nzColor="magenta" class="text-[10px] font-medium rounded-full m-0 flex items-center gap-1">
                      <span nz-icon nzType="lock"></span>
                      <span>Private</span>
                    </nz-tag>
                  } @else {
                    <nz-tag nzColor="blue" class="text-[10px] font-medium rounded-full m-0 flex items-center gap-1">
                      <span nz-icon nzType="user"></span>
                      <span>Shared</span>
                    </nz-tag>
                  }
                </div>
              </div>

              <!-- Balance Display -->
              <div class="mt-4 pt-3 border-t border-slate-100 flex items-baseline justify-between">
                <div>
                  <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider block">Current Balance</span>
                  <span
                    class="text-xl font-bold font-mono tracking-tight"
                    [ngClass]="acc.currentBalancePaise < 0 ? 'text-rose-600' : 'text-slate-900'">
                    {{ acc.currentBalancePaise | inrCurrency }}
                  </span>
                </div>

                <!-- Archive Action -->
                <button
                  nz-button
                  nzType="text"
                  nzDanger
                  nz-popconfirm
                  nzPopconfirmTitle="Archive this account? Its transaction history remains intact."
                  (nzOnConfirm)="archiveAccount(acc.id)"
                  class="text-xs text-slate-400 hover:text-rose-600 p-1">
                  Archive
                </button>
              </div>
            </nz-card>
          }
        </div>
      }

      <!-- Create Account Modal -->
      <nz-modal
        [(nzVisible)]="isModalVisible"
        nzTitle="Add New Account"
        [nzOkText]="'Save Account'"
        [nzOkLoading]="isSubmitting"
        (nzOnCancel)="closeCreateModal()"
        (nzOnOk)="submitCreateAccount()">
        <ng-container *nzModalContent>
          <form [formGroup]="accountForm" nz-form nzLayout="vertical" class="space-y-3 pt-2">
            <nz-form-item class="mb-3">
              <nz-form-label nzRequired>Account Name</nz-form-label>
              <nz-form-control nzErrorTip="Account name is required">
                <input nz-input formControlName="name" placeholder="e.g. HDFC Salary, ICICI Savings, Cash Wallet" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-3">
              <nz-form-item class="mb-3">
                <nz-form-label nzRequired>Account Type</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option [nzValue]="1" nzLabel="Savings Account"></nz-option>
                    <nz-option [nzValue]="2" nzLabel="Checking / Current"></nz-option>
                    <nz-option [nzValue]="3" nzLabel="Credit Card"></nz-option>
                    <nz-option [nzValue]="4" nzLabel="Cash / Physical"></nz-option>
                    <nz-option [nzValue]="5" nzLabel="Loan / Liability"></nz-option>
                    <nz-option [nzValue]="6" nzLabel="Investment / Demat"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item class="mb-3">
                <nz-form-label>Visibility</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="visibility">
                    <nz-option [nzValue]="1" nzLabel="Household (Shared)"></nz-option>
                    <nz-option [nzValue]="2" nzLabel="Private (Only Me)"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <nz-form-item class="mb-3">
                <nz-form-label>Opening Balance (₹)</nz-form-label>
                <nz-form-control>
                  <nz-input-number
                    formControlName="openingBalanceRupees"
                    [nzMin]="0"
                    [nzStep]="100"
                    class="w-full">
                  </nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item class="mb-3">
                <nz-form-label>Account Mask</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="accountNumberMask" placeholder="e.g. •••• 5678" />
                </nz-form-control>
              </nz-form-item>
            </div>
          </form>
        </ng-container>
      </nz-modal>
    </div>
  `
})
export class AccountsComponent implements OnInit {
  accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private msg = inject(NzMessageService);

  isModalVisible = false;
  isSubmitting = false;

  accountForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    type: [1, [Validators.required]],
    visibility: [1, [Validators.required]],
    openingBalanceRupees: [0, [Validators.min(0)]],
    accountNumberMask: ['']
  });

  ngOnInit(): void {
    this.accountService.loadAccounts().subscribe();
  }

  openCreateModal(): void {
    this.accountForm.reset({
      name: '',
      type: 1,
      visibility: 1,
      openingBalanceRupees: 0,
      accountNumberMask: ''
    });
    this.isModalVisible = true;
  }

  closeCreateModal(): void {
    this.isModalVisible = false;
  }

  submitCreateAccount(): void {
    if (this.accountForm.invalid) {
      Object.values(this.accountForm.controls).forEach(control => {
        if (control.invalid) {
          control.markAsDirty();
          control.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    const val = this.accountForm.value;
    const openingBalancePaise = Math.round(Number(val.openingBalanceRupees || 0) * 100);

    this.isSubmitting = true;
    this.accountService.createAccount({
      name: val.name!,
      type: Number(val.type),
      openingBalancePaise,
      currency: 'INR',
      accountNumberMask: val.accountNumberMask?.trim() || undefined,
      visibility: Number(val.visibility)
    }).subscribe({
      next: () => {
        this.msg.success('Account created successfully');
        this.isSubmitting = false;
        this.isModalVisible = false;
      },
      error: err => {
        this.msg.error(err.error?.detail || 'Failed to create account');
        this.isSubmitting = false;
      }
    });
  }

  archiveAccount(id: string): void {
    this.accountService.archiveAccount(id).subscribe({
      next: () => this.msg.success('Account archived'),
      error: () => this.msg.error('Failed to archive account')
    });
  }

  getAccountIcon(type: number | string): string {
    const t = Number(type);
    switch (t) {
      case 1: return 'bank'; // Savings
      case 2: return 'wallet'; // Checking
      case 3: return 'credit-card'; // CreditCard
      case 4: return 'dollar'; // Cash
      case 5: return 'fall'; // Loan
      case 6: return 'rise'; // Investment
      default: return 'bank';
    }
  }

  getTypeName(type: number | string): string {
    const t = Number(type);
    switch (t) {
      case 1: return 'Savings';
      case 2: return 'Checking';
      case 3: return 'Credit Card';
      case 4: return 'Cash';
      case 5: return 'Loan';
      case 6: return 'Investment';
      default: return 'Account';
    }
  }

  getTypeColor(type: number | string): string {
    const t = Number(type);
    switch (t) {
      case 1: return 'green';
      case 2: return 'cyan';
      case 3: return 'orange';
      case 4: return 'gold';
      case 5: return 'red';
      case 6: return 'purple';
      default: return 'default';
    }
  }
}


import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export type AccountType = 'Savings' | 'Checking' | 'CreditCard' | 'Cash' | 'Loan' | 'Investment';
export type VisibilityLevel = 'Household' | 'Private' | 'SelectedMembers';

export interface Account {
  id: string;
  name: string;
  type: number | string; // enum or string
  currentBalancePaise: number;
  currency: string;
  accountNumberMask?: string;
  visibility: number | string;
  isOwner: boolean;
  ownerFullName?: string;
  isArchived: boolean;
  createdAt: string;
}

export interface CreateAccountRequest {
  name: string;
  type: number;
  openingBalancePaise: number;
  currency?: string;
  accountNumberMask?: string;
  visibility?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/accounts';

  accounts = signal<Account[]>([]);
  loading = signal<boolean>(false);

  totalNetWorth = computed(() => {
    return this.accounts()
      .filter(a => !a.isArchived)
      .reduce((sum, a) => {
        // If loan or credit card, it's a liability (though stored balances can be represented accordingly)
        // Here, checking/savings/cash are positive assets
        return sum + Number(a.currentBalancePaise);
      }, 0);
  });

  loadAccounts(): Observable<Account[]> {
    this.loading.set(true);
    return this.http.get<Account[]>(this.baseUrl).pipe(
      tap({
        next: list => {
          this.accounts.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  createAccount(payload: CreateAccountRequest): Observable<Account> {
    return this.http.post<Account>(this.baseUrl, payload).pipe(
      tap(created => {
        this.accounts.update(list => [...list, created]);
      })
    );
  }

  archiveAccount(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.accounts.update(list => list.filter(a => a.id !== id));
      })
    );
  }
}


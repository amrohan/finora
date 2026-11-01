import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface Loan {
  id: string;
  accountId: string;
  accountName: string;
  name: string;
  originalPrincipalPaise: number;
  currentPrincipalPaise: number;
  annualInterestRate: number;
  tenureMonths: number;
  monthlyEmiPaise: number;
  startDate: string;
  createdAt: string;
}

export interface AmortizationItem {
  monthIndex: number;
  paymentDate: string;
  emiPaise: number;
  principalPaise: number;
  interestPaise: number;
  remainingPrincipalPaise: number;
}

export interface CreateLoanRequest {
  name: string;
  originalPrincipalPaise: number;
  annualInterestRate: number;
  tenureMonths: number;
  startDate: string;
  accountId?: string;
}

export interface RecordLoanPaymentRequest {
  paymentAccountId: string;
  paymentAmountPaise: number;
  paymentDate: string;
  customPrincipalPaise?: number;
  customInterestPaise?: number;
  notes?: string;
}

@Injectable({
  providedIn: 'root'
})
export class LoanService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/loans';

  loans = signal<Loan[]>([]);
  loading = signal<boolean>(false);

  totalDebtPaise = computed(() => {
    return this.loans().reduce((sum, l) => sum + Number(l.currentPrincipalPaise), 0);
  });

  loadLoans(): Observable<Loan[]> {
    this.loading.set(true);
    return this.http.get<Loan[]>(this.baseUrl).pipe(
      tap({
        next: list => {
          this.loans.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  createLoan(payload: CreateLoanRequest): Observable<Loan> {
    return this.http.post<Loan>(this.baseUrl, payload).pipe(
      tap(created => {
        this.loans.update(list => [...list, created]);
      })
    );
  }

  recordPayment(loanId: string, payload: RecordLoanPaymentRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${loanId}/payments`, payload).pipe(
      tap(() => {
        this.loadLoans().subscribe();
      })
    );
  }

  getAmortization(loanId: string): Observable<AmortizationItem[]> {
    return this.http.get<AmortizationItem[]>(`${this.baseUrl}/${loanId}/amortization`);
  }

  deleteLoan(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.loans.update(list => list.filter(l => l.id !== id));
      })
    );
  }
}


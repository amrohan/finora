import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface SavingsGoal {
  id: string;
  name: string;
  targetAmountPaise: number;
  currentAmountPaise: number;
  progressPercentage: number;
  targetDate?: string;
  isCompleted: boolean;
  visibility: number;
  createdAt: string;
}

export interface CreateSavingsGoalRequest {
  name: string;
  targetAmountPaise: number;
  targetDate?: string;
  initialContributionPaise?: number;
  sourceAccountId?: string;
  visibility?: number;
}

export interface ContributeToGoalRequest {
  sourceAccountId: string;
  amountPaise: number;
  contributionDate: string;
  notes?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GoalService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/goals';

  goals = signal<SavingsGoal[]>([]);
  loading = signal<boolean>(false);

  totalSavingsPaise = computed(() => {
    return this.goals().reduce((sum, g) => sum + Number(g.currentAmountPaise), 0);
  });

  loadGoals(): Observable<SavingsGoal[]> {
    this.loading.set(true);
    return this.http.get<SavingsGoal[]>(this.baseUrl).pipe(
      tap({
        next: list => {
          this.goals.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  createGoal(payload: CreateSavingsGoalRequest): Observable<SavingsGoal> {
    return this.http.post<SavingsGoal>(this.baseUrl, payload).pipe(
      tap(created => {
        this.goals.update(list => [...list, created]);
      })
    );
  }

  contribute(goalId: string, payload: ContributeToGoalRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/${goalId}/contributions`, payload).pipe(
      tap(() => {
        this.loadGoals().subscribe();
      })
    );
  }

  deleteGoal(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.goals.update(list => list.filter(g => g.id !== id));
      })
    );
  }
}


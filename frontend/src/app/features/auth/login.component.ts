import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzAlertModule
  ],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-100 p-4">
      <nz-card class="w-full max-w-md shadow-lg rounded-2xl border-0">
        <div class="text-center mb-8">
          <div class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-emerald-600 text-white font-bold text-2xl mb-3 shadow-md">
            🪙
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-slate-900">Sign in to Finora</h1>
          <p class="text-sm text-slate-500 mt-1">Family wealth & finance management</p>
        </div>

        @if (errorMessage()) {
          <nz-alert
            nzType="error"
            [nzMessage]="errorMessage()!"
            nzShowIcon
            class="mb-6 rounded-lg">
          </nz-alert>
        }

        <form nz-form [formGroup]="loginForm" (ngSubmit)="onSubmit()" nzLayout="vertical">
          <nz-form-item>
            <nz-form-label nzRequired>Email Address</nz-form-label>
            <nz-form-control nzErrorTip="Please enter a valid email address">
              <input
                nz-input
                formControlName="email"
                type="email"
                placeholder="name@example.com"
                class="rounded-lg h-10" />
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Password</nz-form-label>
            <nz-form-control nzErrorTip="Password is required">
              <input
                nz-input
                formControlName="password"
                type="password"
                placeholder="••••••••"
                class="rounded-lg h-10" />
            </nz-form-control>
          </nz-form-item>

          <button
            nz-button
            nzType="primary"
            nzBlock
            [nzLoading]="isLoading()"
            class="mt-4 h-11 bg-emerald-600 hover:bg-emerald-500 border-0 font-medium rounded-lg text-base shadow-sm">
            Sign In
          </button>
        </form>

        <div class="mt-6 text-center text-xs text-slate-500 space-y-2">
          <p>Registration is invitation-only.</p>
          <p>
            <a routerLink="/auth/setup-admin" class="text-emerald-600 hover:underline">
              Initial server administrator setup →
            </a>
          </p>
        </div>
      </nz-card>
    </div>
  `
})
export class LoginComponent {
  loginForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      Object.values(this.loginForm.controls).forEach(control => {
        if (control.invalid) {
          control.markAsDirty();
          control.updateValueAndValidity({ onlySelf: true });
        }
      });
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.loginForm.value).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: err => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.detail || 'Invalid email or password.');
      }
    });
  }
}


import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class PrivacyService {
  readonly isMasked = signal<boolean>(false);

  toggle(): void {
    this.isMasked.update(v => !v);
  }

  setMasked(masked: boolean): void {
    this.isMasked.set(masked);
  }
}


import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('renders only bootstrap-authorised navigation', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/platform/bootstrap').flush({ clientName: 'Acme', productName: 'Acme Hub', navigation: [{label:'Documents',route:'/documents',feature:'documents',permission:'documents.view'}], featureFlags:{documents:true}, themeTokens:{primary:'#123'}, logo:'/logo.svg', favicon:'/favicon.svg', currentUser:{displayName:'Dev',permissions:['documents.view']} });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Operational intelligence');
    expect(fixture.nativeElement.querySelectorAll('nav a').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('.capabilities article').length).toBe(1);
  });
});

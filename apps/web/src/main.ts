import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AppComponent } from './app/app.component';

const correlationInterceptor = (request: import('@angular/common/http').HttpRequest<unknown>, next: import('@angular/common/http').HttpHandlerFn) =>
  next(request.clone({ setHeaders: { 'X-Correlation-ID': crypto.randomUUID() } }));
bootstrapApplication(AppComponent, { providers: [provideHttpClient(withInterceptors([correlationInterceptor]))] })
  .catch((error: unknown) => console.error(error));

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';
import { WeatherApiContext } from '../api/WeatherApiContext';
import type { WeatherApi } from '../api/weatherApi';

export function renderWithProviders(ui: ReactElement, api: WeatherApi, initialPath = '/') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchInterval: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <WeatherApiContext value={api}>
        <MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>
      </WeatherApiContext>
    </QueryClientProvider>,
  );
}

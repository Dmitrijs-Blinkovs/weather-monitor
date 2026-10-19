import { useQuery } from '@tanstack/react-query';
import { useWeatherApi } from '../api/WeatherApiContext';

const FIVE_MINUTES = 5 * 60_000;

/** Every city that has been fetched at least once, alphabetically. */
export function useCities() {
  const api = useWeatherApi();
  return useQuery({
    queryKey: ['cities'],
    queryFn: ({ signal }) => api.getCities(signal),
    staleTime: FIVE_MINUTES,
  });
}

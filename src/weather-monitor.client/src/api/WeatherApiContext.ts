import { createContext, useContext } from 'react';
import { weatherApi, type WeatherApi } from './weatherApi';

/** Components depend on the API through this context, so tests can supply a fake implementation. */
export const WeatherApiContext = createContext<WeatherApi>(weatherApi);

export const useWeatherApi = (): WeatherApi => useContext(WeatherApiContext);

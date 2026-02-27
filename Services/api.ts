// Base API Configuration
import axios from 'axios';

// Base URL - Environment variable'dan okunur
// Development: Boş string (proxy kullanılır - package.json)
// Production: https://api.fgstrade.com (veya backend sunucu URL'i)
export const API_BASE_URL = process.env.REACT_APP_API_URL || '';

// Axios instance oluştur
export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 600000, // 10 dakika (600000ms) - Scraping uzun sürebilir
});

// Request interceptor - Her istekte token ekle (login/register hariç)
apiClient.interceptors.request.use(
  (config) => {
    // Login ve Register endpoint'lerine token ekleme (public endpoints)
    const publicEndpoints = ['/api/auth/login', '/api/auth/register'];
    const isPublicEndpoint = publicEndpoints.some(endpoint => config.url?.includes(endpoint));
    
    if (!isPublicEndpoint) {
      const token = localStorage.getItem('token');
      
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
    }
    return config;
  },
  (error) => {
    console.error('❌ Request Interceptor Hatası:', error);
    return Promise.reject(error);
  }
);

// Response interceptor - Hata yönetimi
apiClient.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    // 401 hatası - Token geçersiz veya eksik
    if (error.response?.status === 401) {
      console.warn('⚠️ 401 Unauthorized - Token geçersiz veya eksik');
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

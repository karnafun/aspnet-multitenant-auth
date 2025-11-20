import { createContext, useContext, useState, useEffect, useRef } from 'react';

import api, { setAccessToken as setAxiosToken } from '../api/axios';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
    const [accessToken, setAccessToken] = useState(null);
    const [user, setUser] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const refreshTimeoutRef = useRef(null);


    const scheduleTokenRefresh = (expiresIn) => {
        if (refreshTimeoutRef.current) {
            clearTimeout(refreshTimeoutRef.current);
        }
        const refreshTime = (expiresIn * 0.9) * 1000;

        console.log(`Scheduling token refresh in ${refreshTime / 1000} seconds`);

        refreshTimeoutRef.current = setTimeout(async () => {
            try {
                console.log('Silent refresh triggered');
                const { data } = await api.post('/api/auth/refresh');
                setAccessToken(data.accessToken);
                setAxiosToken(data.accessToken);

                scheduleTokenRefresh(data.expiresIn);
            } catch (error) {
                localStorage.removeItem('hasSession');
            }
        }, refreshTime);
    };
    const login = async (email, password, tenant) => {
        setIsLoading(true);

        try {
            const { data } = await api.post('/api/auth/login', {
                email,
                password,
                tenantIdentifier: tenant,
            });

            setAccessToken(data.accessToken);
            setAxiosToken(data.accessToken);

            const userResponse = await api.get('/api/users/me', {
                headers: {
                    Authorization: `Bearer ${data.accessToken}`,
                },
            });

            setUser(userResponse.data);
            localStorage.setItem('hasSession', 'true');
            scheduleTokenRefresh(data.expiresIn);
            setIsLoading(false);

        } catch (error) {
            setIsLoading(false);
            throw new Error(error.response?.data?.message || 'Login failed');
        }
    };
    const logout = async () => {
        if (refreshTimeoutRef.current) {
            clearTimeout(refreshTimeoutRef.current);
        }
        try {
            await api.post('/api/auth/revoke');
        } catch (error) {
            console.error('Logout error:', error);
        } finally {
            setAccessToken(null);
            setAxiosToken(null);
            setUser(null);
            localStorage.removeItem('hasSession');
        }
    };
    useEffect(() => {
        const initAuth = async () => {
            if (!localStorage.getItem('hasSession')) {
                setIsLoading(false);
                return;
            }

            try {
                const { data } = await api.post('/api/auth/refresh');
                setAccessToken(data.accessToken);
                setAxiosToken(data.accessToken);

                const userResponse = await api.get('/api/users/me', {
                    headers: {
                        Authorization: `Bearer ${data.accessToken}`,
                    },
                });

                setUser(userResponse.data);
                scheduleTokenRefresh(data.expiresIn);

            } catch (error) {
                console.log("Error on refresh: "+error)
                localStorage.removeItem('hasSession');
                console.log('Session expired');
            } finally {
                setIsLoading(false);
            }
        };

        initAuth();
    }, []);
    const value = {
        accessToken,
        user,
        isLoading,
        login,
        logout,
    };

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
}

// Custom hook to use the Auth Context
export function useAuth() {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }

    return context;
}
import { createContext, useContext, useState } from "react";
const authContext = createContext(null);

export function AuthProvider({children}){
    const [token, setToken] = useState(null);
    const [email, setEmail] = useState(null);

    const login = (newToken, userEmail) =>{
        setToken(newToken);
        setEmail(userEmail);
    };

    const logout = () => {
        setToken(null);
        setEmail(null);
    };

    return(
        <authContext.Provider value = {{token, email, login, logout}}>
            {children}
        </authContext.Provider>
    );
}

export function useAuth(){
    return useContext(authContext);
}
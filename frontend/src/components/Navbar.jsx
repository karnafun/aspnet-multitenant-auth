import { Link } from "react-router-dom";
import { useAuth } from '../contexts/AuthContext';

export default function Navbar() {
    const { user, logout } = useAuth();


    const emailLabel = user
        ? `${user.email}${user.roles?.includes("Admin") ? " [ADMIN]" : ""}`
        : "";

    return (

        <nav className="w-full border-b border-primary-border bg-primary-surface px-4 py-3 flex justify-between items-center">
            {/* Left side */}
            <div className="flex items-center gap-6">
                <Link
                    to="/projects"
                    className="text-text-primary hover:text-accent-green transition-colors"
                >
                    Home
                </Link>
                {
                    user?.roles?.includes('Admin') && (

                        <Link
                            to="/dashboard"
                            className="text-text-primary hover:text-accent-green transition-colors"
                        >
                           Admin dashboard
                        </Link>
                    )}
            </div>

            {/* Right side */}
            <div className="flex items-center gap-4">
                {user && (
                    <span className="text-text-primary font-medium">
                        {emailLabel}
                    </span>
                )}

                {user && (
                    <button
                        onClick={logout}
                        className="btn btn-secondary"
                    >
                        Logout
                    </button>
                )}
            </div>
        </nav>
    );
}
import { useEffect, useState, Fragment } from "react";
import { Disclosure, Transition } from "@headlessui/react";
import { ChevronRightIcon } from "@heroicons/react/24/solid";
import { adminsApi } from "../api/adminsApi";

export default function DashboardPage() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const fetchUsers = async () => {
    try {
      setLoading(true);
      const res = await adminsApi.getAllUsers();
      setUsers(res);
    } catch (err) {
      setError(err.message || "Failed to fetch users");
    } finally {
      setLoading(false);
    }
  };

  const fetchUserDetails = async (email) => {
    try {
      const res = await adminsApi.getUserByEmail(email);
      setUsers((prev) =>
        prev.map((u) => (u.email === email ? { ...u, details: res } : u))
      );
    } catch (err) {
      // Silently fail - user can retry by clicking again
    }
  };

  useEffect(() => {
    fetchUsers();
  }, []);

  if (loading)
    return (
      <div className="min-h-screen flex items-center justify-center">⏳ Loading...</div>
    );

  if (error)
    return <div className="min-h-screen p-8 text-red-500">{error}</div>;

  return (
    <div className="min-h-screen p-8">
      <div className="max-w-6xl mx-auto space-y-4">
        <h1 className="text-3xl font-bold mb-4">User Management</h1>

        {users.length === 0 ? (
          <p>No users found.</p>
        ) : (
          users.map((user) => (
            <Disclosure key={user.id} as="div" className="card">
              {({ open }) => (
                <>
                  <Disclosure.Button
                    className="flex justify-between items-center w-full p-4 cursor-pointer"
                    onClick={async () => {
                      if (!user.details) await fetchUserDetails(user.email);
                    }}
                  >
                    <div>
                      <p className="font-semibold">{user.username}</p>
                      <p className="text-sm text-text-secondary">{user.email}</p>
                    </div>

                    <div className="flex items-center gap-2">
                      <p className="text-sm text-text-secondary">
                        Last Login: {new Date(user.lastLoginAt).toLocaleString()}
                      </p>
                      <ChevronRightIcon
                        className={`w-5 h-5 text-text-secondary transform transition-transform duration-300 ${
                          open ? "rotate-90" : "rotate-0"
                        }`}
                      />
                    </div>
                  </Disclosure.Button>

                  <Transition
                    as={Fragment}
                    enter="transition ease-out duration-300"
                    enterFrom="transform scale-y-0 opacity-0"
                    enterTo="transform scale-y-100 opacity-100"
                    leave="transition ease-in duration-200"
                    leaveFrom="transform scale-y-100 opacity-100"
                    leaveTo="transform scale-y-0 opacity-0"
                  >
                    <Disclosure.Panel className="p-4 border-t border-primary-border space-y-2">
                      {user.details ? (
                        <>
                          <p>Email Confirmed: {user.details.emailConfirmed ? "true" : "false"}</p>
                          <p>
                            Phone: {user.details.phoneNumber} (
                            {user.details.phoneNumberConfirmed ? "true" : "false"})
                          </p>
                          <p>Created At: {new Date(user.details.createdAt).toLocaleString()}</p>
                          <p>isDeleted: {user.details.isDeleted ? "true" : "false"}</p>
                          <p>Tenant ID: {user.details.tenantId}</p>

                          <div className="mt-2">
                            <h4 className="font-semibold mb-1">Projects Watching:</h4>
                            {user.details.projectsWatching?.length > 0 ? (
                              <ul className="list-disc list-inside">
                                {user.details.projectsWatching.map((p) => (
                                  <li key={p.id}>
                                    {p.title} ({p.status}) - Assigned: {p.assignedTo} - Created by:{" "}
                                    {p.createdByName}
                                  </li>
                                ))}
                              </ul>
                            ) : (
                              <p className="text-text-secondary">No projects watching</p>
                            )}
                          </div>
                        </>
                      ) : (
                        <p className="text-text-secondary">Loading details...</p>
                      )}
                    </Disclosure.Panel>
                  </Transition>
                </>
              )}
            </Disclosure>
          ))
        )}
      </div>
    </div>
  );
}

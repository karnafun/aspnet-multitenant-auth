/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          bg: '#0A0E27',
          surface: '#1A1F3A',
          border: '#2A2F4A',
        },
        text: {
          primary: '#E8E9ED',
          secondary: '#9BA0B5',
        },
        accent: {
          green: '#00D4AA',
          'green-hover': '#00B896',
        },
        error: '#F5455C',
      },
    },
  },
  plugins: [],
}
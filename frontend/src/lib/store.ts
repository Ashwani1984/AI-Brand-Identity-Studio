"use client";

import { configureStore, createSlice, PayloadAction } from "@reduxjs/toolkit";
import { AuthResponse } from "./api";

type AuthState = {
  user: AuthResponse | null;
};

const authSlice = createSlice({
  name: "auth",
  initialState: { user: null } as AuthState,
  reducers: {
    setUser(state, action: PayloadAction<AuthResponse | null>) {
      state.user = action.payload;
    },
  },
});

export const { setUser } = authSlice.actions;

export const makeStore = () =>
  configureStore({
    reducer: { auth: authSlice.reducer },
  });

export type AppStore = ReturnType<typeof makeStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];

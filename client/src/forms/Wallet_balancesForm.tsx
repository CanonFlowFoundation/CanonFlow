import React from 'react';
import { useForm } from 'react-hook-form';
import * as Validators from '../validators';

export default function Wallet_balancesForm() {
  const { register, handleSubmit, formState: { errors } } = useForm();

  const onSubmit = (data: any) => {
    // Automatically apply CanonFlow mathematical validators before submission
    // NOTE: This assumes an aggregate validator is emitted. For now, pseudo-code.
    console.log("Validated Data:", data);
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="p-6 bg-white rounded shadow-md">
      <h2 className="text-xl font-bold mb-4">Wallet_balancesForm</h2>
      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">wallet_id</label>
        <input 
          type="text" 
          {...register('wallet_id')} 
          className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
        />
        {errors.wallet_id && <p className="text-red-500 text-xs mt-1">{(errors.wallet_id as any).message}</p>}
      </div>
      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">available_balance</label>
        <input 
          type="number" 
          {...register('available_balance', { validate: (v, formValues) => Validators.validate_wallet_balances_available_balance(formValues) || 'Invalid value' })} 
          className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
        />
        {errors.available_balance && <p className="text-red-500 text-xs mt-1">{(errors.available_balance as any).message}</p>}
      </div>
      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">locked_balance</label>
        <input 
          type="number" 
          {...register('locked_balance', { validate: (v, formValues) => Validators.validate_wallet_balances_locked_balance(formValues) || 'Invalid value' })} 
          className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
        />
        {errors.locked_balance && <p className="text-red-500 text-xs mt-1">{(errors.locked_balance as any).message}</p>}
      </div>
      <div className="mb-4">
        <label className="block text-sm font-medium text-gray-700">updated_at</label>
        <input 
          type="text" 
          {...register('updated_at')} 
          className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
        />
        {errors.updated_at && <p className="text-red-500 text-xs mt-1">{(errors.updated_at as any).message}</p>}
      </div>
      <button type="submit" className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">Submit</button>
    </form>
  );
}
